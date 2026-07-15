namespace Zero72.Blog.Reading;

public sealed record ReadingBook(
    Guid Id,
    string Title,
    string Author,
    string CoverTone,
    string? CoverImageUrl,
    int RecordCount,
    decimal TotalHours,
    DateOnly? FirstReadDate,
    DateOnly? LastReadDate);
