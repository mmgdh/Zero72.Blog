namespace Zero72.Blog.Reading;

public sealed class ReadingBookEntity
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public string CoverTone { get; set; } = "midnight";

    public string? CoverImageUrl { get; set; }

    public ICollection<ReadingRecordEntity> Records { get; set; } = [];
}
