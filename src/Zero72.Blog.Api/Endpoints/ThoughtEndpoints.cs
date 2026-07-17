using Zero72.Blog.Infrastructure;
using Zero72.Blog.Shared;

namespace Zero72.Blog.Api.Endpoints;

/// <summary>
/// 注册公开思考时间线和管理员思考记录接口。
/// </summary>
public static class ThoughtEndpoints
{
    /// <summary>
    /// 注册公开查询以及需要管理员身份的增删改查端点。
    /// </summary>
    public static IEndpointRouteBuilder MapThoughtEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/thoughts", async (IThoughtStore store, CancellationToken cancellationToken) =>
            Results.Ok(await store.GetPublishedAsync(cancellationToken)))
            .WithTags("Thoughts");

        var adminGroup = app.MapGroup("/api/admin/thoughts")
            .WithTags("Admin Thoughts")
            .RequireAuthorization();

        adminGroup.MapGet("/", async (IThoughtStore store, CancellationToken cancellationToken) =>
            Results.Ok(await store.GetAllAsync(cancellationToken)));

        adminGroup.MapGet("/{id:guid}", async (
            Guid id,
            IThoughtStore store,
            CancellationToken cancellationToken) =>
        {
            var item = await store.GetAsync(id, cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        adminGroup.MapPost("/", async (
            SaveThoughtRequest request,
            IThoughtStore store,
            CancellationToken cancellationToken) =>
        {
            var validation = Validate(request);
            if (validation is not null)
            {
                return validation;
            }

            var item = await store.CreateAsync(request, cancellationToken);
            return Results.Created($"/api/admin/thoughts/{item.Id}", item);
        });

        adminGroup.MapPut("/{id:guid}", async (
            Guid id,
            SaveThoughtRequest request,
            IThoughtStore store,
            CancellationToken cancellationToken) =>
        {
            var validation = Validate(request);
            if (validation is not null)
            {
                return validation;
            }

            var item = await store.UpdateAsync(id, request, cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        adminGroup.MapDelete("/{id:guid}", async (
            Guid id,
            IThoughtStore store,
            CancellationToken cancellationToken) =>
        {
            return await store.DeleteAsync(id, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound();
        });

        return app;
    }

    /// <summary>
    /// 校验思考正文、时间和图片地址长度。
    /// </summary>
    private static IResult? Validate(SaveThoughtRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Results.BadRequest("Thought content is required.");
        }

        if (request.Content.Length > 2000)
        {
            return Results.BadRequest("Thought content must be 2000 characters or fewer.");
        }

        if (request.OccurredAt == default)
        {
            return Results.BadRequest("Occurred time is required.");
        }

        if (request.ImageUrl?.Length > 500)
        {
            return Results.BadRequest("Image URL is too long.");
        }

        return null;
    }
}
