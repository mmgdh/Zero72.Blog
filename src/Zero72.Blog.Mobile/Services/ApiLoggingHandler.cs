using System.Diagnostics;

namespace Zero72.Blog.Mobile.Services;

/// <summary>
/// 记录博客 HTTP 请求的地址、状态码、耗时和响应摘要。
/// 不记录请求正文、Cookie 或认证标头，避免管理员密码进入日志。
/// </summary>
internal sealed class ApiLoggingHandler(AppLogService logs, HttpMessageHandler innerHandler)
    : DelegatingHandler(innerHandler)
{
    private const int MaxBodyPreviewCharacters = 2_000;

    /// <summary>
    /// 执行请求并记录成功响应或网络异常。
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var method = request.Method.Method;
        var url = request.RequestUri?.AbsoluteUri ?? "（未知地址）";
        var stopwatch = Stopwatch.StartNew();
        await logs.InfoAsync("API", $"请求 {method} {url}").ConfigureAwait(false);

        try
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "（无 Content-Type）";
            var contentLength = response.Content.Headers.ContentLength?.ToString() ?? "未知";
            var summary = $"响应 {method} {url} -> {(int)response.StatusCode} {response.ReasonPhrase}，耗时 {stopwatch.ElapsedMilliseconds} ms，类型 {contentType}，长度 {contentLength}";
            await logs.InfoAsync("API", summary).ConfigureAwait(false);
            await LogResponseBodyAsync(response, contentType).ConfigureAwait(false);
            return response;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            await logs.ErrorAsync("API", $"请求超时 {method} {url}，耗时 {stopwatch.ElapsedMilliseconds} ms").ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            await logs.ErrorAsync("API", $"请求异常 {method} {url}，耗时 {stopwatch.ElapsedMilliseconds} ms：{exception.GetType().Name}：{exception.Message}").ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// 对 JSON、文本和错误响应记录正文摘要；二进制文件只记录响应元数据。
    /// </summary>
    private async Task LogResponseBodyAsync(HttpResponseMessage response, string contentType)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        var isText = mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
            mediaType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
            mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase);
        if (!isText)
        {
            return;
        }

        try
        {
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var preview = body.Length <= MaxBodyPreviewCharacters
                ? body
                : $"{body[..MaxBodyPreviewCharacters]}…（已截断）";
            await logs.InfoAsync("API正文", string.IsNullOrWhiteSpace(preview) ? "（空响应）" : preview).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await logs.WarningAsync("API正文", $"读取响应摘要失败：{exception.Message}").ConfigureAwait(false);
        }
    }
}
