using Microsoft.AspNetCore.Mvc;

namespace Zero72.Blog.Api.Security;

/// <summary>
/// 捕获 API 端点未处理异常，记录完整日志并返回结构化 Problem Details。
/// 已认证的管理请求可以看到最内层异常消息，公开请求仅获得安全的通用说明。
/// </summary>
public static class ApiExceptionMiddlewareExtensions
{
    /// <summary>
    /// 注册统一异常处理中间件，避免 500 响应正文为空。
    /// </summary>
    public static IApplicationBuilder UseApiExceptionDetails(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (Exception exception) when (!context.Response.HasStarted)
            {
                var logger = context.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Zero72.Blog.Api.UnhandledException");
                logger.LogError(
                    exception,
                    "请求 {Method} {Path} 处理失败，跟踪编号 {TraceId}。",
                    context.Request.Method,
                    context.Request.Path,
                    context.TraceIdentifier);

                var canShowInternalDetail = context.User.Identity?.IsAuthenticated == true
                    && context.Request.Path.StartsWithSegments("/api/admin");
                var detail = canShowInternalDetail
                    ? GetInnermostMessage(exception)
                    : "服务器处理请求时发生异常，请稍后重试。";
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "服务器处理请求失败",
                    Detail = detail,
                    Instance = context.Request.Path
                };
                problem.Extensions["traceId"] = context.TraceIdentifier;

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
            }
        });

        return app;
    }

    /// <summary>
    /// 提取异常链最内层且长度受限的消息，避免返回无意义的外层包装描述。
    /// </summary>
    private static string GetInnermostMessage(Exception exception)
    {
        while (exception.InnerException is not null)
        {
            exception = exception.InnerException;
        }

        const int maxLength = 1000;
        return exception.Message.Length <= maxLength
            ? exception.Message
            : $"{exception.Message[..maxLength]}…";
    }
}
