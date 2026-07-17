using Microsoft.EntityFrameworkCore;
using Zero72.Blog.Domain;
using Zero72.Blog.Shared;

namespace Zero72.Blog.Infrastructure;

/// <summary>
/// 使用 Entity Framework Core 持久化思考记录。
/// </summary>
public sealed class EfThoughtStore(BlogDbContext dbContext) : IThoughtStore
{
    /// <summary>
    /// 查询公开记录并按发生时间倒序排列。
    /// </summary>
    public async Task<IReadOnlyList<ThoughtItem>> GetPublishedAsync(CancellationToken cancellationToken = default)
    {
        return await BuildQuery()
            .Where(entry => entry.IsPublished)
            .OrderByDescending(entry => entry.OccurredAt)
            .Select(entry => ToItem(entry))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 查询全部记录并按发生时间倒序排列。
    /// </summary>
    public async Task<IReadOnlyList<ThoughtItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await BuildQuery()
            .OrderByDescending(entry => entry.OccurredAt)
            .Select(entry => ToItem(entry))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 按标识查询单条记录。
    /// </summary>
    public async Task<ThoughtItem?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entry = await BuildQuery().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entry is null ? null : ToItem(entry);
    }

    /// <summary>
    /// 创建并保存一条思考记录。
    /// </summary>
    public async Task<ThoughtItem> CreateAsync(
        SaveThoughtRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var entry = new ThoughtEntry
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            UpdatedAt = now
        };
        Apply(entry, request);
        dbContext.ThoughtEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToItem(entry);
    }

    /// <summary>
    /// 更新指定记录并保存最后修改时间。
    /// </summary>
    public async Task<ThoughtItem?> UpdateAsync(
        Guid id,
        SaveThoughtRequest request,
        CancellationToken cancellationToken = default)
    {
        var entry = await dbContext.ThoughtEntries.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entry is null)
        {
            return null;
        }

        Apply(entry, request);
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToItem(entry);
    }

    /// <summary>
    /// 删除指定思考记录。
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entry = await dbContext.ThoughtEntries.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entry is null)
        {
            return false;
        }

        dbContext.ThoughtEntries.Remove(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// 创建不跟踪实体的基础查询。
    /// </summary>
    private IQueryable<ThoughtEntry> BuildQuery()
    {
        return dbContext.ThoughtEntries.AsNoTracking();
    }

    /// <summary>
    /// 将请求字段规范化后写入实体。
    /// </summary>
    private static void Apply(ThoughtEntry entry, SaveThoughtRequest request)
    {
        entry.Content = request.Content.Trim();
        // PostgreSQL 的 timestamp with time zone 只接受 UTC DateTimeOffset，统一转换可兼容所有客户端时区。
        entry.OccurredAt = request.OccurredAt.ToUniversalTime();
        entry.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
        entry.IsPublished = request.IsPublished;
    }

    /// <summary>
    /// 将数据库实体转换为跨客户端 DTO。
    /// </summary>
    private static ThoughtItem ToItem(ThoughtEntry entry)
    {
        return new ThoughtItem(
            entry.Id,
            entry.Content,
            entry.OccurredAt,
            entry.ImageUrl,
            entry.IsPublished,
            entry.UpdatedAt);
    }
}
