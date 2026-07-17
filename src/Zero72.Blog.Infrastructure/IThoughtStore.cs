using Zero72.Blog.Shared;

namespace Zero72.Blog.Infrastructure;

/// <summary>
/// 定义思考记录的查询和管理操作。
/// </summary>
public interface IThoughtStore
{
    /// <summary>
    /// 查询公开展示的思考记录。
    /// </summary>
    Task<IReadOnlyList<ThoughtItem>> GetPublishedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询包含草稿在内的全部思考记录。
    /// </summary>
    Task<IReadOnlyList<ThoughtItem>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 按标识查询单条思考记录。
    /// </summary>
    Task<ThoughtItem?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建思考记录。
    /// </summary>
    Task<ThoughtItem> CreateAsync(SaveThoughtRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新思考记录，不存在时返回空值。
    /// </summary>
    Task<ThoughtItem?> UpdateAsync(Guid id, SaveThoughtRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除思考记录并返回是否删除成功。
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
