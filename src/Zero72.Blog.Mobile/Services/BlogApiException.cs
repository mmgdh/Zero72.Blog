namespace Zero72.Blog.Mobile.Services;

/// <summary>
/// 表示博客 API 返回的可展示业务错误。
/// </summary>
public sealed class BlogApiException(string message) : Exception(message);
