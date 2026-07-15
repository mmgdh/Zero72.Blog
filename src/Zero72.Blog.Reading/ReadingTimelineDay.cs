namespace Zero72.Blog.Reading;

public sealed record ReadingTimelineDay(
    DateOnly Date,
    string Weekday,
    IReadOnlyList<ReadingRecord> Records,
    IReadOnlyList<string> Insights);
