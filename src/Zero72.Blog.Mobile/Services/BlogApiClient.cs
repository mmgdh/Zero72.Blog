using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Maui.Storage;
using Zero72.Blog.Mobile.Models;
using Zero72.Blog.Reading;
using Zero72.Blog.Shared;

namespace Zero72.Blog.Mobile.Services;

/// <summary>
/// 通过公开博客的 API 代理访问管理员接口，并在进程内维护认证 Cookie。
/// </summary>
public sealed class BlogApiClient : IDisposable
{
    private const int MaxImageBytes = 5 * 1024 * 1024;
    private const string AdminCookieName = "__Zero72Admin";
    private const string SecureSessionKey = "zero72_admin_auth_cookie_v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly MobileSettingsService settings;
    private readonly SessionState session;
    private readonly AppLogService logs;
    private readonly CookieContainer cookieContainer;
    private readonly HttpClient httpClient;
    private readonly SemaphoreSlim sessionRestoreLock = new(1, 1);
    private bool sessionRestoreAttempted;

    /// <summary>
    /// 创建启用 Cookie 的原生 HTTP 客户端。
    /// </summary>
    public BlogApiClient(
        MobileSettingsService settings,
        SessionState session,
        AppLogService logs)
    {
        this.settings = settings;
        this.session = session;
        this.logs = logs;
        cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer = cookieContainer,
            UseCookies = true,
            AutomaticDecompression = DecompressionMethods.All
        };
        httpClient = new HttpClient(new ApiLoggingHandler(logs, handler))
        {
            Timeout = TimeSpan.FromSeconds(45)
        };
    }

    /// <summary>
    /// 查询服务器上的当前认证状态。
    /// </summary>
    public async Task<AdminAuthStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await RestoreSessionAsync(cancellationToken);
        using var response = await SendAsync(HttpMethod.Get, "api/admin/auth/me", cancellationToken);
        var status = await response.Content.ReadFromJsonAsync<AdminAuthStatus>(cancellationToken);
        status ??= new AdminAuthStatus(false, null);
        session.Update(status.IsAuthenticated, status.UserName);
        if (status.IsAuthenticated)
        {
            await PersistSessionAsync();
        }
        else
        {
            await ClearPersistedSessionAsync();
        }

        return status;
    }

    /// <summary>
    /// 使用管理员账号登录并保存返回的认证 Cookie。
    /// </summary>
    public async Task<bool> LoginAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendJsonAsync(
            HttpMethod.Post,
            "api/admin/auth/login",
            new AdminLoginRequest(userName, password),
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.TooManyRequests)
        {
            session.Update(false, null);
            await ClearPersistedSessionAsync();
            return false;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        var status = await response.Content.ReadFromJsonAsync<AdminAuthStatus>(cancellationToken);
        session.Update(status?.IsAuthenticated == true, status?.UserName);
        if (status?.IsAuthenticated == true)
        {
            await PersistSessionAsync();
        }
        else
        {
            await ClearPersistedSessionAsync();
        }

        return status?.IsAuthenticated == true;
    }

    /// <summary>
    /// 注销当前 Cookie 会话。
    /// </summary>
    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await SendAsync(HttpMethod.Post, "api/admin/auth/logout", cancellationToken);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                await EnsureSuccessAsync(response, cancellationToken);
            }
        }
        finally
        {
            session.Update(false, null);
            await ClearPersistedSessionAsync();
        }
    }

    /// <summary>
    /// 获取博客服务器公开的安卓最新版本清单；尚未发布升级包时返回空值。
    /// </summary>
    public async Task<MobileReleaseInfo?> GetLatestReleaseAsync(
        CancellationToken cancellationToken = default)
    {
        var cacheBuster = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildUri($"mobile/latest.json?v={cacheBuster}"));
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.AcceptEncoding.ParseAdd("identity");
        request.Headers.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true
        };
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new BlogApiException(
                "更新服务器返回了空的版本清单。请先完整发布一次博客服务，再重新检查更新。");
        }

        try
        {
            return JsonSerializer.Deserialize<MobileReleaseInfo>(content, JsonOptions);
        }
        catch (JsonException)
        {
            var preview = content.Length <= 160 ? content : $"{content[..160]}…";
            throw new BlogApiException($"更新服务器返回了无效的版本清单：{preview}");
        }
    }

    /// <summary>
    /// 获取全部思考记录，包括草稿。
    /// </summary>
    public Task<List<ThoughtItem>> GetThoughtsAsync(CancellationToken cancellationToken = default)
    {
        return GetListAsync<ThoughtItem>("api/admin/thoughts", cancellationToken);
    }

    /// <summary>
    /// 新增或更新思考记录。
    /// </summary>
    public async Task SaveThoughtAsync(
        Guid? id,
        SaveThoughtRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendJsonAsync(
            id is null ? HttpMethod.Post : HttpMethod.Put,
            id is null ? "api/admin/thoughts" : $"api/admin/thoughts/{id}",
            request,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <summary>
    /// 删除指定思考记录。
    /// </summary>
    public Task DeleteThoughtAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return DeleteAsync($"api/admin/thoughts/{id}", cancellationToken);
    }

    /// <summary>
    /// 获取图书库。
    /// </summary>
    public Task<List<ReadingBook>> GetBooksAsync(CancellationToken cancellationToken = default)
    {
        return GetListAsync<ReadingBook>("api/admin/reading-books", cancellationToken);
    }

    /// <summary>
    /// 新增或更新图书。
    /// </summary>
    public async Task SaveBookAsync(
        Guid? id,
        SaveReadingBookRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendJsonAsync(
            id is null ? HttpMethod.Post : HttpMethod.Put,
            id is null ? "api/admin/reading-books" : $"api/admin/reading-books/{id}",
            request,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <summary>
    /// 删除没有阅读记录的图书。
    /// </summary>
    public Task DeleteBookAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return DeleteAsync($"api/admin/reading-books/{id}", cancellationToken);
    }

    /// <summary>
    /// 获取全部阅读记录。
    /// </summary>
    public Task<List<ReadingRecord>> GetReadingRecordsAsync(CancellationToken cancellationToken = default)
    {
        return GetListAsync<ReadingRecord>("api/admin/reading-records", cancellationToken);
    }

    /// <summary>
    /// 新增或更新阅读记录。
    /// </summary>
    public async Task SaveReadingRecordAsync(
        Guid? id,
        SaveReadingRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendJsonAsync(
            id is null ? HttpMethod.Post : HttpMethod.Put,
            id is null ? "api/admin/reading-records" : $"api/admin/reading-records/{id}",
            request,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <summary>
    /// 删除指定阅读记录。
    /// </summary>
    public Task DeleteReadingRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return DeleteAsync($"api/admin/reading-records/{id}", cancellationToken);
    }

    /// <summary>
    /// 上传不超过服务器限制的图片并返回公开地址。
    /// </summary>
    public async Task<UploadImageResponse> UploadImageAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        using var imageContent = new StreamContent(stream);
        var effectiveContentType = string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType;
        imageContent.Headers.ContentType = new MediaTypeHeaderValue(effectiveContentType);
        form.Add(imageContent, "file", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri("api/admin/assets/images"))
        {
            Content = form
        };
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<UploadImageResponse>(cancellationToken)
            ?? throw new BlogApiException("服务器没有返回有效的图片地址。");
    }

    /// <summary>
    /// 将服务器返回的相对资源地址转换为可显示的绝对地址。
    /// </summary>
    public string? ToPublicUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalizedPath = path.Trim();
        var baseUri = settings.GetBaseUri();
        if (normalizedPath.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        // Uri.TryCreate 会把“/uploads/...”识别为 file:// 绝对地址，因此必须先处理站点根相对路径。
        if (normalizedPath.StartsWith("/", StringComparison.Ordinal) ||
            !Uri.TryCreate(normalizedPath, UriKind.Absolute, out var absolute))
        {
            var serverRoot = new Uri($"{baseUri.Scheme}://{baseUri.Authority}/");
            return new Uri(serverRoot, normalizedPath.TrimStart('/')).AbsoluteUri;
        }

        var isHttp = absolute.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            absolute.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        var isCurrentServer = absolute.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase) &&
            absolute.Port == baseUri.Port;
        return isHttp && isCurrentServer ? absolute.AbsoluteUri : null;
    }

    /// <summary>
    /// 通过原生 HTTP 获取图片并生成 WebView 可直接解码的内嵌 Data URL。
    /// 该链路绕开 Android WebView 对外部 HTTP 子资源的限制，同时记录文件类型和文件头诊断。
    /// </summary>
    public async Task<string> GetImageDataUrlAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        var publicUrl = ToPublicUrl(imagePath);
        if (publicUrl is null)
        {
            throw new BlogApiException($"图片地址无效或不属于当前服务器：{imagePath}");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, publicUrl);
            request.Headers.Accept.ParseAdd("image/avif,image/webp,image/png,image/jpeg,image/*,*/*;q=0.8");
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

            var declaredLength = response.Content.Headers.ContentLength;
            if (declaredLength > MaxImageBytes)
            {
                throw new BlogApiException("图片超过 5 MB，无法在移动端显示。");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (bytes.Length == 0)
            {
                throw new BlogApiException("图片服务器返回了空文件。");
            }

            if (bytes.Length > MaxImageBytes)
            {
                throw new BlogApiException("图片超过 5 MB，无法在移动端显示。");
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrWhiteSpace(contentType) ||
                !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                throw new BlogApiException($"服务器返回的内容不是图片，Content-Type：{contentType ?? "（空）"}。");
            }

            var headerLength = Math.Min(32, bytes.Length);
            var header = bytes.AsSpan(0, headerLength);
            var format = DetectImageFormat(header);
            var hexadecimalHeader = Convert.ToHexString(header);
            await logs.InfoAsync(
                "图片诊断",
                $"原生读取成功：{publicUrl}；类型 {contentType}；长度 {bytes.Length}；识别格式 {format}；前 {headerLength} 字节 {hexadecimalHeader}；将以内嵌 Data URL 显示。")
                .ConfigureAwait(false);

            if (format == "未知")
            {
                await logs.WarningAsync("图片诊断", "响应的文件头不是常见图片格式，WebView 可能无法解码。").ConfigureAwait(false);
            }

            return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
        }
        catch (Exception exception)
        {
            await logs.ErrorAsync("图片诊断", $"读取图片异常：{publicUrl}；{exception.GetType().Name}：{exception.Message}")
                .ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// 释放底层原生 HTTP 客户端。
    /// </summary>
    public void Dispose()
    {
        sessionRestoreLock.Dispose();
        httpClient.Dispose();
    }

    /// <summary>
    /// 查询列表并统一处理身份失效和错误响应。
    /// </summary>
    private async Task<List<T>> GetListAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, path, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<T>>(cancellationToken) ?? [];
    }

    /// <summary>
    /// 删除资源并统一处理错误响应。
    /// </summary>
    private async Task DeleteAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Delete, path, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <summary>
    /// 创建并发送不带正文的 HTTP 请求。
    /// </summary>
    private Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, BuildUri(path));
        return SendOwnedRequestAsync(request, cancellationToken);
    }

    /// <summary>
    /// 创建并发送 JSON 请求。
    /// </summary>
    private Task<HttpResponseMessage> SendJsonAsync<T>(
        HttpMethod method,
        string path,
        T body,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, BuildUri(path))
        {
            Content = JsonContent.Create(body)
        };
        return SendOwnedRequestAsync(request, cancellationToken);
    }

    /// <summary>
    /// 发送请求后释放请求对象，并把响应所有权交给调用者。
    /// </summary>
    private async Task<HttpResponseMessage> SendOwnedRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using (request)
        {
            return await httpClient.SendAsync(request, cancellationToken);
        }
    }

    /// <summary>
    /// 根据当前设置构造 API 绝对地址。
    /// </summary>
    private Uri BuildUri(string path)
    {
        return new Uri(settings.GetBaseUri(), path.TrimStart('/'));
    }

    /// <summary>
    /// 根据文件头识别常见网络图片格式。
    /// </summary>
    private static string DetectImageFormat(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return "JPEG";
        }

        if (header.Length >= 8 &&
            header[0] == 0x89 &&
            header[1] == 0x50 &&
            header[2] == 0x4E &&
            header[3] == 0x47 &&
            header[4] == 0x0D &&
            header[5] == 0x0A &&
            header[6] == 0x1A &&
            header[7] == 0x0A)
        {
            return "PNG";
        }

        if (header.Length >= 12 &&
            header[..4].SequenceEqual("RIFF"u8) &&
            header.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return "WebP";
        }

        if (header.Length >= 6 &&
            (header[..6].SequenceEqual("GIF87a"u8) || header[..6].SequenceEqual("GIF89a"u8)))
        {
            return "GIF";
        }

        if (header.Length >= 12 && header.Slice(4, 8).SequenceEqual("ftypavif"u8))
        {
            return "AVIF";
        }

        return "未知";
    }

    /// <summary>
    /// 将 HTTP 错误转换为用户可读异常，并同步更新登录状态。
    /// </summary>
    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            session.Update(false, null);
            await ClearPersistedSessionAsync();
            throw new BlogApiException("登录已失效，请重新登录。");
        }

        var detail = await ReadErrorDetailAsync(response, cancellationToken);
        var statusDescription = string.IsNullOrWhiteSpace(response.ReasonPhrase)
            ? $"HTTP {(int)response.StatusCode}"
            : $"{(int)response.StatusCode} {response.ReasonPhrase}";
        var message = response.StatusCode == HttpStatusCode.TooManyRequests
            ? "操作过于频繁，请稍后再试。"
            : string.IsNullOrWhiteSpace(detail)
                ? $"服务器返回 {statusDescription}，但没有提供错误详情。"
                : $"服务器返回 {statusDescription}：{detail}";
        throw new BlogApiException(message);
    }

    /// <summary>
    /// 从 Problem Details、JSON 字符串或纯文本响应中提取适合展示的错误详情。
    /// </summary>
    private static async Task<string?> ReadErrorDetailAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.String)
            {
                return root.GetString();
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                var detail = GetJsonString(root, "detail");
                var title = GetJsonString(root, "title");
                var traceId = GetJsonString(root, "traceId");
                var description = !string.IsNullOrWhiteSpace(detail) ? detail : title;
                return string.IsNullOrWhiteSpace(traceId)
                    ? description
                    : $"{description}（跟踪编号：{traceId}）";
            }
        }
        catch (JsonException)
        {
            // 非 JSON 响应继续按纯文本展示。
        }

        return content.Length <= 1000 ? content : $"{content[..1000]}…";
    }

    /// <summary>
    /// 不区分属性是否存在地读取 JSON 字符串值。
    /// </summary>
    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    /// <summary>
    /// 首次请求前从 Android SecureStorage 恢复加密保存的管理员 Cookie。
    /// 恢复后仍必须调用服务端认证状态接口验证，客户端缓存本身不代表已经登录。
    /// </summary>
    private async Task RestoreSessionAsync(CancellationToken cancellationToken)
    {
        await sessionRestoreLock.WaitAsync(cancellationToken);
        try
        {
            if (sessionRestoreAttempted)
            {
                return;
            }

            sessionRestoreAttempted = true;
            var serialized = await SecureStorage.Default.GetAsync(SecureSessionKey);
            if (string.IsNullOrWhiteSpace(serialized))
            {
                return;
            }

            var stored = JsonSerializer.Deserialize<PersistedAuthSession>(serialized, JsonOptions);
            var baseUri = settings.GetBaseUri();
            var currentOrigin = baseUri.GetLeftPart(UriPartial.Authority);
            if (stored is null ||
                !string.Equals(stored.Origin, currentOrigin, StringComparison.OrdinalIgnoreCase) ||
                stored.ExpiresUtc <= DateTimeOffset.UtcNow ||
                string.IsNullOrWhiteSpace(stored.CookieValue))
            {
                await ClearPersistedSessionAsync();
                return;
            }

            var cookie = new Cookie(AdminCookieName, stored.CookieValue, "/", baseUri.Host)
            {
                Expires = stored.ExpiresUtc.UtcDateTime,
                HttpOnly = true,
                Secure = baseUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            };
            cookieContainer.Add(baseUri, cookie);
            await logs.InfoAsync("认证", $"已从安全存储恢复登录 Cookie，有效期至 {stored.ExpiresUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}，正在向服务器验证。");
        }
        catch (Exception exception)
        {
            SecureStorage.Default.Remove(SecureSessionKey);
            await logs.WarningAsync("认证", $"恢复登录 Cookie 失败，已忽略本地缓存：{exception.GetType().Name}：{exception.Message}");
        }
        finally
        {
            sessionRestoreLock.Release();
        }
    }

    /// <summary>
    /// 将服务端签发或滑动续期后的 Cookie 加密保存到 Android SecureStorage。
    /// </summary>
    private async Task PersistSessionAsync()
    {
        try
        {
            var baseUri = settings.GetBaseUri();
            var cookie = cookieContainer
                .GetCookies(baseUri)
                .Cast<Cookie>()
                .FirstOrDefault(item => item.Name.Equals(AdminCookieName, StringComparison.Ordinal));
            if (cookie is null || cookie.Expired || string.IsNullOrWhiteSpace(cookie.Value))
            {
                await logs.WarningAsync("认证", "服务器认证成功，但没有找到可持久化的登录 Cookie。");
                return;
            }

            var expiresUtc = cookie.Expires == DateTime.MinValue
                ? DateTimeOffset.UtcNow.AddDays(7)
                : new DateTimeOffset(cookie.Expires.ToUniversalTime());
            var stored = new PersistedAuthSession(
                baseUri.GetLeftPart(UriPartial.Authority),
                cookie.Value,
                expiresUtc);
            await SecureStorage.Default.SetAsync(
                SecureSessionKey,
                JsonSerializer.Serialize(stored, JsonOptions));
            await logs.InfoAsync("认证", $"登录 Cookie 已加密保存，有效期至 {expiresUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}。");
        }
        catch (Exception exception)
        {
            await logs.WarningAsync("认证", $"保存登录 Cookie 失败，本次运行仍可继续使用：{exception.GetType().Name}：{exception.Message}");
        }
    }

    /// <summary>
    /// 删除安全存储和 CookieContainer 中的当前管理员会话。
    /// </summary>
    private Task ClearPersistedSessionAsync()
    {
        SecureStorage.Default.Remove(SecureSessionKey);
        var baseUri = settings.GetBaseUri();
        var expiredCookie = new Cookie(AdminCookieName, string.Empty, "/", baseUri.Host)
        {
            Expires = DateTime.UtcNow.AddYears(-1),
            Expired = true
        };
        cookieContainer.Add(baseUri, expiredCookie);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 表示加密保存到设备安全存储中的最小管理员会话信息。
    /// </summary>
    private sealed record PersistedAuthSession(
        string Origin,
        string CookieValue,
        DateTimeOffset ExpiresUtc);
}
