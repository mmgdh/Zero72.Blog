using System.Net.Http.Headers;

/// <summary>
/// 负责浏览器 API 反向代理与服务端渲染内容请求的统一配置。
/// </summary>
public static class ApiProxyEndpoints
{
    private const string HttpClientName = "ApiProxy";
    private const string PublicContentHttpClientName = "PublicContentApi";
    private const string DefaultProxySecretHeaderName = "X-Zero72-Proxy-Secret";

    private static readonly string[] ProxyMethods =
    [
        HttpMethods.Delete,
        HttpMethods.Get,
        HttpMethods.Head,
        HttpMethods.Options,
        HttpMethods.Patch,
        HttpMethods.Post,
        HttpMethods.Put
    ];

    private static readonly HashSet<string> SkippedRequestHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection",
        "Host",
        "Keep-Alive",
        "Proxy-Connection",
        "TE",
        "Trailer",
        "Transfer-Encoding",
        "Upgrade"
    };

    /// <summary>
    /// 注册供反向代理端点使用的命名 HTTP 客户端。
    /// </summary>
    public static IServiceCollection AddApiProxy(this IServiceCollection services, IConfiguration configuration)
    {
        var apiBaseUrl = configuration["Proxy:ApiBaseUrl"];
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            return services;
        }

        services.AddHttpClient(HttpClientName, client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        return services;
    }

    /// <summary>
    /// 注册服务端渲染页面使用的公开内容客户端，并在内部请求中附加代理凭据。
    /// </summary>
    public static IServiceCollection AddPublicContentApiClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var apiBaseUrl = configuration["Proxy:ApiBaseUrl"] ?? "http://localhost:5000";
        var proxySecret = configuration["Proxy:Secret"];
        var proxyHeaderName = configuration["Proxy:HeaderName"] ?? DefaultProxySecretHeaderName;

        services.AddHttpClient(PublicContentHttpClientName, client =>
        {
            client.BaseAddress = new Uri(EnsureTrailingSlash(apiBaseUrl), UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(30);

            if (!string.IsNullOrWhiteSpace(proxySecret))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(proxyHeaderName, proxySecret);
            }
        });
        services.AddScoped(serviceProvider =>
            serviceProvider
                .GetRequiredService<IHttpClientFactory>()
                .CreateClient(PublicContentHttpClientName));

        return services;
    }

    /// <summary>
    /// 将公开 API 与上传资源路径转发到内部 API 服务。
    /// </summary>
    public static IEndpointRouteBuilder MapApiProxy(this IEndpointRouteBuilder app, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration["Proxy:ApiBaseUrl"]))
        {
            return app;
        }

        app.MapMethods("/api/{**path}", ProxyMethods, ProxyAsync);
        app.MapMethods("/uploads/{**path}", ProxyMethods, ProxyAsync);
        return app;
    }

    /// <summary>
    /// 转发单次 HTTP 请求并将内部服务响应流式写回浏览器。
    /// </summary>
    private static async Task ProxyAsync(
        HttpContext context,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        var targetUri = new Uri(client.BaseAddress!, context.Request.Path.ToString() + context.Request.QueryString);

        using var proxyRequest = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUri);
        CopyRequestHeaders(context, proxyRequest, configuration);

        if (HasRequestBody(context.Request))
        {
            proxyRequest.Content = new StreamContent(context.Request.Body);
            CopyContentHeaders(context.Request, proxyRequest.Content.Headers);
        }

        using var response = await client.SendAsync(
            proxyRequest,
            HttpCompletionOption.ResponseHeadersRead,
            context.RequestAborted);

        context.Response.StatusCode = (int)response.StatusCode;
        CopyResponseHeaders(context, response);

        if (response.Content is not null)
        {
            await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
    }

    /// <summary>
    /// 复制安全的请求头，并补充客户端来源和内部代理凭据。
    /// </summary>
    private static void CopyRequestHeaders(
        HttpContext context,
        HttpRequestMessage proxyRequest,
        IConfiguration configuration)
    {
        foreach (var header in context.Request.Headers)
        {
            if (SkippedRequestHeaders.Contains(header.Key) ||
                header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            proxyRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }

        var remoteIp = context.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrWhiteSpace(remoteIp))
        {
            proxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-For", remoteIp);
        }

        proxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-Host", context.Request.Host.Value);
        proxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-Proto", context.Request.Scheme);

        var proxySecret = configuration["Proxy:Secret"];
        if (!string.IsNullOrWhiteSpace(proxySecret))
        {
            var headerName = configuration["Proxy:HeaderName"] ?? DefaultProxySecretHeaderName;
            proxyRequest.Headers.Remove(headerName);
            proxyRequest.Headers.TryAddWithoutValidation(headerName, proxySecret);
        }
    }

    /// <summary>
    /// 复制请求正文相关的内容头。
    /// </summary>
    private static void CopyContentHeaders(HttpRequest request, HttpContentHeaders targetHeaders)
    {
        foreach (var header in request.Headers)
        {
            if (header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
            {
                targetHeaders.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }
    }

    /// <summary>
    /// 将内部 API 的响应头复制到对外响应。
    /// </summary>
    private static void CopyResponseHeaders(HttpContext context, HttpResponseMessage response)
    {
        foreach (var header in response.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        foreach (var header in response.Content.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        context.Response.Headers.Remove("transfer-encoding");
    }

    /// <summary>
    /// 判断当前请求是否携带需要转发的正文。
    /// </summary>
    private static bool HasRequestBody(HttpRequest request)
    {
        return request.ContentLength > 0 ||
            request.Headers.TransferEncoding.Any(value => string.Equals(value, "chunked", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 统一 API 基地址格式，确保页面中的相对请求不会覆盖最后一级路径。
    /// </summary>
    private static string EnsureTrailingSlash(string value)
    {
        return value.EndsWith("/", StringComparison.Ordinal) ? value : $"{value}/";
    }
}
