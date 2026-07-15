using Zero72.Blog.Reading;

namespace Zero72.Blog.Api.Endpoints;

public static class ReadingRecordEndpoints
{
    public static IEndpointRouteBuilder MapReadingBookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reading-books").WithTags("Reading Books");

        group.MapGet("/", (IReadingLibraryStore store) =>
        {
            return Results.Ok(store.GetBooks());
        });

        group.MapGet("/{id:guid}", (Guid id, IReadingLibraryStore store) =>
        {
            var book = store.GetBook(id);
            return book is null ? Results.NotFound() : Results.Ok(book);
        });

        return app;
    }

    public static IEndpointRouteBuilder MapReadingRecordEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reading-records").WithTags("Reading Records");

        group.MapGet("/", (DateOnly? from, DateOnly? to, Guid? bookId, IReadingLibraryStore store) =>
        {
            var records = store.GetRecords(from, to, bookId);
            var timeline = ReadingTimeline.FromRecords(records);
            return Results.Ok(timeline);
        });

        return app;
    }

    public static IEndpointRouteBuilder MapAdminReadingBookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/reading-books").WithTags("Admin Reading Books");

        group.MapGet("/", (IReadingLibraryStore store) =>
        {
            return Results.Ok(store.GetBooks());
        });

        group.MapGet("/{id:guid}", (Guid id, IReadingLibraryStore store) =>
        {
            var book = store.GetBook(id);
            return book is null ? Results.NotFound() : Results.Ok(book);
        });

        group.MapPost("/", (SaveReadingBookRequest request, IReadingLibraryStore store) =>
        {
            var validation = ValidateBook(request);
            if (validation is not null)
            {
                return validation;
            }

            var book = store.CreateBook(request);
            return Results.Created($"/api/admin/reading-books/{book.Id}", book);
        });

        group.MapPut("/{id:guid}", (Guid id, SaveReadingBookRequest request, IReadingLibraryStore store) =>
        {
            var validation = ValidateBook(request);
            if (validation is not null)
            {
                return validation;
            }

            var book = store.UpdateBook(id, request);
            return book is null ? Results.NotFound() : Results.Ok(book);
        });

        group.MapDelete("/{id:guid}", (Guid id, IReadingLibraryStore store) =>
        {
            return store.DeleteBook(id)
                ? Results.NoContent()
                : Results.BadRequest("Book does not exist or still has reading records.");
        });

        return app;
    }

    public static IEndpointRouteBuilder MapAdminReadingRecordEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/reading-records").WithTags("Admin Reading Records");

        group.MapGet("/", (IReadingLibraryStore store) =>
        {
            return Results.Ok(store.GetRecords());
        });

        group.MapGet("/{id:guid}", (Guid id, IReadingLibraryStore store) =>
        {
            var record = store.GetRecord(id);
            return record is null ? Results.NotFound() : Results.Ok(record);
        });

        group.MapPost("/", (SaveReadingRecordRequest request, IReadingLibraryStore store) =>
        {
            var validation = ValidateRecord(request);
            if (validation is not null)
            {
                return validation;
            }

            var record = store.CreateRecord(request);
            return record is null
                ? Results.BadRequest("Selected book does not exist.")
                : Results.Created($"/api/admin/reading-records/{record.Id}", record);
        });

        group.MapPut("/{id:guid}", (Guid id, SaveReadingRecordRequest request, IReadingLibraryStore store) =>
        {
            var validation = ValidateRecord(request);
            if (validation is not null)
            {
                return validation;
            }

            var record = store.UpdateRecord(id, request);
            return record is null ? Results.NotFound() : Results.Ok(record);
        });

        group.MapDelete("/{id:guid}", (Guid id, IReadingLibraryStore store) =>
        {
            return store.DeleteRecord(id) ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }

    private static IResult? ValidateBook(SaveReadingBookRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Results.BadRequest("Book title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Author))
        {
            return Results.BadRequest("Author is required.");
        }

        if (request.CoverImageUrl?.Length > 500)
        {
            return Results.BadRequest("Cover image URL is too long.");
        }

        return null;
    }

    private static IResult? ValidateRecord(SaveReadingRecordRequest request)
    {
        if (request.BookId == Guid.Empty)
        {
            return Results.BadRequest("Book is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Chapter))
        {
            return Results.BadRequest("Chapter is required.");
        }

        if (request.FinishedAt <= request.StartedAt)
        {
            return Results.BadRequest("Finished time must be later than started time.");
        }

        return null;
    }
}
