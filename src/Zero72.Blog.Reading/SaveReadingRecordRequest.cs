namespace Zero72.Blog.Reading;

public sealed record SaveReadingRecordRequest(
    DateOnly ReadDate,
    Guid BookId,
    string Chapter,
    TimeOnly StartedAt,
    TimeOnly FinishedAt,
    IReadOnlyList<string> Reflections);
