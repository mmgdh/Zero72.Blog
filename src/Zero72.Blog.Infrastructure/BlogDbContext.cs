using Microsoft.EntityFrameworkCore;
using Zero72.Blog.Domain;
using Zero72.Blog.Reading;

namespace Zero72.Blog.Infrastructure;

public sealed class BlogDbContext(DbContextOptions<BlogDbContext> options) : DbContext(options)
{
    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();

    public DbSet<ReadingBookEntity> ReadingBooks => Set<ReadingBookEntity>();

    public DbSet<ReadingRecordEntity> ReadingRecords => Set<ReadingRecordEntity>();

    public DbSet<ThoughtEntry> ThoughtEntries => Set<ThoughtEntry>();

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

        modelBuilder.Entity<ReadingBookEntity>(entity =>
        {
            entity.ToTable("reading_books");
            entity.HasKey(book => book.Id);
            entity.HasIndex(book => new { book.Title, book.Author }).IsUnique();

            entity.Property(book => book.Title).HasMaxLength(200).IsRequired();
            entity.Property(book => book.Author).HasMaxLength(160).IsRequired();
            entity.Property(book => book.CoverTone).HasMaxLength(40).IsRequired();
            entity.Property(book => book.CoverImageUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<ReadingRecordEntity>(entity =>
        {
            entity.ToTable("reading_records");
            entity.HasKey(record => record.Id);
            entity.HasIndex(record => record.ReadDate);
            entity.HasIndex(record => record.BookId);

            entity.Property(record => record.BookId).IsRequired();
            entity.Property(record => record.ReadDate).IsRequired();
            entity.Property(record => record.Chapter).HasMaxLength(300).IsRequired();
            entity.Property(record => record.StartedAt).IsRequired();
            entity.Property(record => record.FinishedAt).IsRequired();
            entity.Property(record => record.DurationHours).HasPrecision(4, 1).IsRequired();
            entity.Property(record => record.Reflections).IsRequired();

            entity.HasOne(record => record.Book)
                .WithMany(book => book.Records)
                .HasForeignKey(record => record.BookId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
        });

        modelBuilder.Entity<ThoughtEntry>(entity =>
        {
            entity.ToTable("thought_entries");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.OccurredAt);
            entity.HasIndex(item => item.IsPublished);

            entity.Property(item => item.Content).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.OccurredAt).IsRequired();
            entity.Property(item => item.ImageUrl).HasMaxLength(500);
            entity.Property(item => item.Tags).HasColumnType("text[]").IsRequired();
            entity.Property(item => item.IsPublished).IsRequired();
            entity.Property(item => item.CreatedAt).IsRequired();
            entity.Property(item => item.UpdatedAt).IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }
}
