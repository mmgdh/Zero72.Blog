using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.FileProviders;
using Zero72.Blog.ClientHost.Components;

var appRoot = AppContext.BaseDirectory;
var webRoot = Path.Combine(appRoot, "wwwroot");
var mobileRoot = Path.Combine(webRoot, "mobile");
Directory.CreateDirectory(mobileRoot);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = appRoot,
    WebRootPath = webRoot
});

var useServerRendering = !string.Equals(
    builder.Configuration["Hosting:Mode"],
    "Static",
    StringComparison.OrdinalIgnoreCase);

builder.Services.AddApiProxy(builder.Configuration);
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
    [
        "application/wasm",
        "application/octet-stream"
    ]);
});

if (useServerRendering)
{
    builder.Services
        .AddRazorComponents()
        .AddInteractiveServerComponents();
    builder.Services.AddPublicContentApiClient(builder.Configuration);
}

var app = builder.Build();

var contentTypeProvider = new FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".dat"] = "application/octet-stream";
contentTypeProvider.Mappings[".dll"] = "application/octet-stream";
contentTypeProvider.Mappings[".pdb"] = "application/octet-stream";
contentTypeProvider.Mappings[".wasm"] = "application/wasm";
contentTypeProvider.Mappings[".apk"] = "application/vnd.android.package-archive";

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/mobile"))
    {
        // MapStaticAssets 在部分 Accept-Encoding 组合下会把小型 JSON 清单返回为空正文。
        // 安卓升级文件不需要压缩，因此在进入响应压缩中间件前强制使用原始传输。
        context.Request.Headers.Remove("Accept-Encoding");
    }

    context.Response.OnStarting(() =>
    {
        ApplySecurityHeaders(context.Response);

        if ((context.Request.Path.Value ?? string.Empty).EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.ContentType = "application/vnd.android.package-archive";
        }

        if (ShouldUseImmutableCache(context.Request.Path))
        {
            ApplyImmutableCache(context.Response);
        }
        else if (ShouldAvoidCache(context.Request.Path))
        {
            ApplyNoStore(context.Response);
        }

        return Task.CompletedTask;
    });

    await next();
});

// 安卓升级文件由独立物理目录提供，允许运行中的容器接收新 APK，无需重新构建博客镜像。
// 该中间件必须位于响应压缩之前，避免小型 JSON 清单在部分压缩协商下被错误返回为空正文。
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mobileRoot),
    RequestPath = "/mobile",
    ContentTypeProvider = contentTypeProvider,
    ServeUnknownFileTypes = false,
    OnPrepareResponse = context =>
    {
        if (ShouldUseImmutableCache(context.Context.Request.Path))
        {
            ApplyImmutableCache(context.Context.Response);
            return;
        }

        ApplyNoStore(context.Context.Response);
    }
});

app.UseResponseCompression();

if (useServerRendering)
{
    app.UseAntiforgery();
}

app.MapApiProxy(builder.Configuration);

if (!useServerRendering)
{
    app.UseDefaultFiles();
    app.UseStaticFiles(new StaticFileOptions
    {
        ContentTypeProvider = contentTypeProvider,
        ServeUnknownFileTypes = true,
        DefaultContentType = "application/octet-stream",
        OnPrepareResponse = context =>
        {
            if (ShouldUseImmutableCache(context.Context.Request.Path))
            {
                ApplyImmutableCache(context.Context.Response);
                return;
            }

            if (ShouldAvoidCache(context.Context.Request.Path))
            {
                ApplyNoStore(context.Context.Response);
            }
        }
    });
}

if (useServerRendering)
{
    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode()
        .AddAdditionalAssemblies(typeof(Zero72.Blog.Client.App).Assembly);
}
else
{
    app.MapFallbackToFile("index.html");
}

app.Run();

// 为所有站点响应设置基础浏览器安全头。
static void ApplySecurityHeaders(HttpResponse response)
{
    response.Headers["X-Content-Type-Options"] = "nosniff";
    response.Headers["X-Frame-Options"] = "DENY";
    response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
}

// 对 HTML、配置和路由响应禁用缓存，避免部署后继续使用旧入口。
static void ApplyNoStore(HttpResponse response)
{
    response.Headers.CacheControl = "no-store, no-cache, max-age=0, must-revalidate";
    response.Headers.Pragma = "no-cache";
    response.Headers.Expires = "0";
}

// 对带版本标识的框架与库资源设置长期不可变缓存。
static void ApplyImmutableCache(HttpResponse response)
{
    response.Headers.CacheControl = "public, max-age=31536000, immutable";
    response.Headers.Remove("Pragma");
    response.Headers.Remove("Expires");
}

// 判断静态模式下的资源是否可以长期缓存。
static bool ShouldUseImmutableCache(PathString path)
{
    var value = path.Value ?? string.Empty;
    return path.StartsWithSegments("/_framework") ||
        path.StartsWithSegments("/lib") ||
        (path.StartsWithSegments("/mobile") &&
            value.EndsWith(".apk", StringComparison.OrdinalIgnoreCase));
}

// 判断请求是否属于必须即时刷新的页面入口或运行时配置。
static bool ShouldAvoidCache(PathString path)
{
    var value = path.Value ?? string.Empty;
    if (string.IsNullOrEmpty(value) || value == "/")
    {
        return true;
    }

    if (value.EndsWith("index.html", StringComparison.OrdinalIgnoreCase) ||
        value.EndsWith("appsettings.json", StringComparison.OrdinalIgnoreCase) ||
        value.EndsWith("appsettings.Development.json", StringComparison.OrdinalIgnoreCase) ||
        value.EndsWith("/mobile/latest.json", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    return !Path.HasExtension(value);
}
