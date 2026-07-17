namespace Zero72.Blog.Shared;

public sealed record AdminAuthStatus(bool IsAuthenticated, string? UserName);
