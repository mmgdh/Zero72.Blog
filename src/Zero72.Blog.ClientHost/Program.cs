using Microsoft.AspNetCore.StaticFiles;

var appRoot = AppContext.BaseDirectory;
var webRoot = Path.Combine(appRoot, "wwwroot");

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = appRoot,
    WebRootPath = webRoot
});

var app = builder.Build();

var contentTypeProvider = new FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".dat"] = "application/octet-stream";
contentTypeProvider.Mappings[".dll"] = "application/octet-stream";
contentTypeProvider.Mappings[".pdb"] = "application/octet-stream";
contentTypeProvider.Mappings[".wasm"] = "application/wasm";

var frameworkRoot = Path.GetFullPath(Path.Combine(app.Environment.WebRootPath, "_framework"));

app.MapGet("/_framework/{**assetPath}", (string assetPath, HttpContext httpContext) =>
{
    var requestedPath = Path.GetFullPath(Path.Combine(frameworkRoot, assetPath));
    if (!requestedPath.StartsWith(frameworkRoot, StringComparison.OrdinalIgnoreCase) ||
        !System.IO.File.Exists(requestedPath))
    {
        return Results.NotFound();
    }

    if (!contentTypeProvider.TryGetContentType(requestedPath, out var contentType))
    {
        contentType = "application/octet-stream";
    }

    httpContext.Response.Headers.CacheControl = "no-store";
    return Results.File(requestedPath, contentType, enableRangeProcessing: true);
});

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider,
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream",
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.CacheControl = "no-store";
    }
});

app.MapFallbackToFile("index.html");

app.Run();
