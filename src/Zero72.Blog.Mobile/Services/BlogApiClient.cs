using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Zero72.Blog.Mobile.Models;
using Zero72.Blog.Reading;
using Zero72.Blog.Shared;

namespace Zero72.Blog.Mobile.Services;

/// <summary>
/// 通过公开博客的 API 代理访问管理员接口，并在进程内维护认证 Cookie。
/// </summary>
public sealed class BlogApiClient : IDisposable
{
    private readonly MobileSettingsService settings;
    private readonly SessionState session;
    private readonly HttpClient httpClient;

    /// <summary>
    /// 创建启用 Cookie 的原生 HTTP 客户端。
    /// </summary>
    public BlogApiClient(MobileSettingsService settings, SessionState session)
    {
        this.settings = settings;
        this.session = session;
        var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            AutomaticDecompression = DecompressionMethods.All
        };
        httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(45)
        };
    }

    /// <summary>
    /// 查询服务器上的当前认证状态。
    /// </summary>
    public async Task<AdminAuthStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/admin/auth/me", cancellationToken);
        var status = await response.Content.ReadFromJsonAsync<AdminAuthStatus>(cancellationToken);
        status ??= new AdminAuthStatus(false, null);
        session.Update(status.IsAuthenticated, status.UserName);
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
            return false;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        var status = await response.Content.ReadFromJsonAsync<AdminAuthStatus>(cancellationToken);
        session.Update(status?.IsAuthenticated == true, status?.UserName);
        return status?.IsAuthenticated == true;
    }

    /// <summary>
    /// 注销当前 Cookie 会话。
    /// </summary>
    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/admin/auth/logout", cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            await EnsureSuccessAsync(response, cancellationToken);
        }

        session.Update(false, null);
    }

    /// <summary>
    /// 获取博客服务器公开的安卓最新版本清单；尚未发布升级包时返回空值。
    /// </summary>
    public async Task<MobileReleaseInfo?> GetLatestReleaseAsync(
        CancellationToken cancellationToken = default)
    {
        var cacheBuster = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var response = await SendAsync(
            HttpMethod.Get,
            $"mobile/latest.json?v={cacheBuster}",
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<MobileReleaseInfo>(cancellationToken);
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

        var baseUri = settings.GetBaseUri();
        if (!Uri.TryCreate(path.Trim(), UriKind.Absolute, out var absolute))
        {
            return new Uri(baseUri, path.Trim().TrimStart('/')).AbsoluteUri;
        }

        var isHttp = absolute.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            absolute.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        var isCurrentServer = absolute.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase) &&
            absolute.Port == baseUri.Port;
        return isHttp && isCurrentServer ? absolute.AbsoluteUri : null;
    }

    /// <summary>
    /// 释放底层原生 HTTP 客户端。
    /// </summary>
    public void Dispose()
    {
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
}
