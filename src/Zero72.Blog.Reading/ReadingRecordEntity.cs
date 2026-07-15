namespace Zero72.Blog.Reading;

public sealed class ReadingRecordEntity
{
    public Guid Id { get; set; }

    public Guid BookId { get; set; }

    public ReadingBookEntity? Book { get; set; }

    public DateOnly ReadDate { get; set; }

    public string Chapter { get; set; } = string.Empty;

    public TimeOnly StartedAt { get; set; }

    public TimeOnly FinishedAt { get; set; }

    public decimal DurationHours { get; set; }

    public string[] Reflections { get; set; } = [];
}
