namespace Zero72.Blog.Shared;

/// <summary>
/// 集中定义感悟标签的数量、长度限制以及统一的清理规则。
/// </summary>
public static class ThoughtTagRules
{
    /// <summary>
    /// 单条感悟允许保存的最大标签数量。
    /// </summary>
    public const int MaxTagCount = 8;

    /// <summary>
    /// 单个标签允许保存的最大字符数。
    /// </summary>
    public const int MaxTagLength = 20;

    /// <summary>
    /// 移除空标签、首尾空白和重复项，同时保留用户首次输入的显示形式。
    /// </summary>
    public static string[] Normalize(IEnumerable<string>? tags)
    {
        return tags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
    }
}
