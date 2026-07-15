using Microsoft.EntityFrameworkCore;
using Zero72.Blog.Reading;

namespace Zero72.Blog.Infrastructure;

public sealed class EfReadingLibraryStore(BlogDbContext dbContext) : IReadingLibraryStore
{
    public IReadOnlyList<ReadingBook> GetBooks()
    {
        return dbContext.ReadingBooks
            .AsNoTracking()
            .Include(book => book.Records)
            .OrderBy(book => book.Title)
            .ThenBy(book => book.Author)
            .Select(ToBook)
            .ToList();
    }

    public ReadingBook? GetBook(Guid id)
    {
        var entity = dbContext.ReadingBooks
            .AsNoTracking()
            .Include(book => book.Records)
            .FirstOrDefault(book => book.Id == id);

        return entity is null ? null : ToBook(entity);
    }

    public ReadingBook CreateBook(SaveReadingBookRequest request)
    {
        var entity = new ReadingBookEntity
        {
            Id = Guid.NewGuid()
        };

        ApplyBook(entity, request);
        dbContext.ReadingBooks.Add(entity);
        dbContext.SaveChanges();
        return ToBook(entity);
    }

    public ReadingBook? UpdateBook(Guid id, SaveReadingBookRequest request)
    {
        var entity = dbContext.ReadingBooks
            .Include(book => book.Records)
            .FirstOrDefault(book => book.Id == id);

        if (entity is null)
        {
            return null;
        }

        ApplyBook(entity, request);
        dbContext.SaveChanges();
        return ToBook(entity);
    }

    public bool DeleteBook(Guid id)
    {
        var entity = dbContext.ReadingBooks
            .Include(book => book.Records)
            .FirstOrDefault(book => book.Id == id);

        if (entity is null || entity.Records.Count > 0)
        {
            return false;
        }

        dbContext.ReadingBooks.Remove(entity);
        dbContext.SaveChanges();
        return true;
    }

    public IReadOnlyList<ReadingRecord> GetRecords(DateOnly? from = null, DateOnly? to = null, Guid? bookId = null)
    {
        var query = dbContext.ReadingRecords
            .AsNoTracking()
            .Include(record => record.Book)
            .AsQueryable();

        if (from is not null || to is not null)
        {
            var startDate = from ?? DateOnly.MinValue;
            var endDate = to ?? DateOnly.MaxValue;
            if (startDate > endDate)
            {
                (startDate, endDate) = (endDate, startDate);
            }

            query = query.Where(record => record.ReadDate >= startDate && record.ReadDate <= endDate);
        }

        if (bookId is not null)
        {
            query = query.Where(record => record.BookId == bookId);
        }

        return query
            .OrderByDescending(record => record.ReadDate)
            .ThenBy(record => record.StartedAt)
            .Select(ToRecord)
            .ToList();
    }

    public ReadingRecord? GetRecord(Guid id)
    {
        var entity = dbContext.ReadingRecords
            .AsNoTracking()
            .Include(record => record.Book)
            .FirstOrDefault(record => record.Id == id);

        return entity is null ? null : ToRecord(entity);
    }

    public ReadingRecord? CreateRecord(SaveReadingRecordRequest request)
    {
        if (!dbContext.ReadingBooks.Any(book => book.Id == request.BookId))
        {
            return null;
        }

        var entity = new ReadingRecordEntity { Id = Guid.NewGuid() };
        ApplyRecord(entity, request);
        dbContext.ReadingRecords.Add(entity);
        dbContext.SaveChanges();
        dbContext.Entry(entity).Reference(record => record.Book).Load();
        return ToRecord(entity);
    }

    public ReadingRecord? UpdateRecord(Guid id, SaveReadingRecordRequest request)
    {
        if (!dbContext.ReadingBooks.Any(book => book.Id == request.BookId))
        {
            return null;
        }

        var entity = dbContext.ReadingRecords
            .Include(record => record.Book)
            .FirstOrDefault(record => record.Id == id);

        if (entity is null)
        {
            return null;
        }

        ApplyRecord(entity, request);
        dbContext.SaveChanges();
        dbContext.Entry(entity).Reference(record => record.Book).Load();
        return ToRecord(entity);
    }

    public bool DeleteRecord(Guid id)
    {
        var entity = dbContext.ReadingRecords.FirstOrDefault(record => record.Id == id);
        if (entity is null)
        {
            return false;
        }

        dbContext.ReadingRecords.Remove(entity);
        dbContext.SaveChanges();
        return true;
    }

    private static ReadingBook ToBook(ReadingBookEntity entity)
    {
        return new ReadingBook(
            entity.Id,
            entity.Title,
            entity.Author,
            entity.CoverTone,
            entity.CoverImageUrl,
            entity.Records.Count,
            entity.Records.Sum(record => record.DurationHours),
            entity.Records.Count == 0 ? null : entity.Records.Min(record => record.ReadDate),
            entity.Records.Count == 0 ? null : entity.Records.Max(record => record.ReadDate));
    }

    private static ReadingRecord ToRecord(ReadingRecordEntity entity)
    {
        var book = entity.Book ?? new ReadingBookEntity();

        return new ReadingRecord(
            entity.Id,
            entity.BookId,
            entity.ReadDate,
            book.Title,
            book.Author,
            entity.Chapter,
            entity.StartedAt,
            entity.FinishedAt,
            entity.DurationHours,
            entity.Reflections,
            book.CoverTone,
            book.CoverImageUrl);
    }

    private static void ApplyBook(ReadingBookEntity entity, SaveReadingBookRequest request)
    {
        entity.Title = request.Title.Trim();
        entity.Author = request.Author.Trim();
        entity.CoverTone = string.IsNullOrWhiteSpace(request.CoverTone) ? "midnight" : request.CoverTone.Trim();
        entity.CoverImageUrl = string.IsNullOrWhiteSpace(request.CoverImageUrl) ? null : request.CoverImageUrl.Trim();
    }

    private static void ApplyRecord(ReadingRecordEntity entity, SaveReadingRecordRequest request)
    {
        entity.BookId = request.BookId;
        entity.ReadDate = request.ReadDate;
        entity.Chapter = request.Chapter.Trim();
        entity.StartedAt = request.StartedAt;
        entity.FinishedAt = request.FinishedAt;
        entity.DurationHours = Math.Round((decimal)(request.FinishedAt - request.StartedAt).TotalHours, 1);
        entity.Reflections = request.Reflections
            .Where(reflection => !string.IsNullOrWhiteSpace(reflection))
            .Select(reflection => reflection.Trim())
            .ToArray();
    }
}
