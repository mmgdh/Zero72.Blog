using Microsoft.EntityFrameworkCore;
using Zero72.Blog.Domain;

namespace Zero72.Blog.Infrastructure;

public sealed class BlogDbContext(DbContextOptions<BlogDbContext> options) : DbContext(options)
{
    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BlogPost>(entity =>
        {
            entity.ToTable("blog_posts");
            entity.HasKey(post => post.Id);
            entity.HasIndex(post => post.Slug).IsUnique();

            entity.Property(post => post.Title).HasMaxLength(200).IsRequired();
            entity.Property(post => post.Slug).HasMaxLength(220).IsRequired();
            entity.Property(post => post.Summary).HasMaxLength(500).IsRequired();
            entity.Property(post => post.ContentMarkdown).IsRequired();
            entity.Property(post => post.CreatedAt).IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }
}
