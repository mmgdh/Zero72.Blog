namespace Zero72.Blog.Reading;

public sealed record SaveReadingBookRequest(
    string Title,
    string Author,
    string CoverTone,
    string? CoverImageUrl);
