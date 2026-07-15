namespace Zero72.Blog.Reading;

public sealed record ReadingRecord(
    Guid Id,
    Guid BookId,
    DateOnly ReadDate,
    string BookTitle,
    string Author,
    string Chapter,
    TimeOnly StartedAt,
    TimeOnly FinishedAt,
    decimal DurationHours,
    IReadOnlyList<string> Reflections,
    string CoverTone,
    string? CoverImageUrl);
