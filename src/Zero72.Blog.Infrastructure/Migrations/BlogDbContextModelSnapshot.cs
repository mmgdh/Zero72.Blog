using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Zero72.Blog.Infrastructure.Migrations;

[DbContext(typeof(BlogDbContext))]
partial class BlogDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.9")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        modelBuilder.Entity("Zero72.Blog.Domain.BlogPost", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uuid");

            entity.Property<string>("ContentMarkdown")
                .IsRequired()
                .HasColumnType("text");

            entity.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone");

            entity.Property<bool>("IsPublished")
                .HasColumnType("boolean");

            entity.Property<DateTimeOffset?>("PublishedAt")
                .HasColumnType("timestamp with time zone");

            entity.Property<string>("Slug")
                .IsRequired()
                .HasMaxLength(220)
                .HasColumnType("character varying(220)");

            entity.Property<string>("Summary")
                .IsRequired()
                .HasMaxLength(500)
                .HasColumnType("character varying(500)");

            entity.Property<string>("Title")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("character varying(200)");

            entity.HasKey("Id");
            entity.HasIndex("Slug").IsUnique();
            entity.ToTable("blog_posts");
        });

        modelBuilder.Entity("Zero72.Blog.Domain.ThoughtEntry", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uuid");

            entity.Property<string>("Content")
                .IsRequired()
                .HasMaxLength(2000)
                .HasColumnType("character varying(2000)");

            entity.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone");

            entity.Property<string>("ImageUrl")
                .HasMaxLength(500)
                .HasColumnType("character varying(500)");

            entity.Property<bool>("IsPublished")
                .HasColumnType("boolean");

            entity.Property<DateTimeOffset>("OccurredAt")
                .HasColumnType("timestamp with time zone");

            entity.Property<string[]>("Tags")
                .IsRequired()
                .HasColumnType("text[]");

            entity.Property<DateTimeOffset>("UpdatedAt")
                .HasColumnType("timestamp with time zone");

            entity.HasKey("Id");

            entity.HasIndex("IsPublished");

            entity.HasIndex("OccurredAt");

            entity.ToTable("thought_entries");
        });

        modelBuilder.Entity("Zero72.Blog.Reading.ReadingBookEntity", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uuid");

            entity.Property<string>("Author")
                .IsRequired()
                .HasMaxLength(160)
                .HasColumnType("character varying(160)");

            entity.Property<string>("CoverTone")
                .IsRequired()
                .HasMaxLength(40)
                .HasColumnType("character varying(40)");

            entity.Property<string>("CoverImageUrl")
                .HasMaxLength(500)
                .HasColumnType("character varying(500)");

            entity.Property<string>("Title")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("character varying(200)");

            entity.HasKey("Id");

            entity.HasIndex("Title", "Author").IsUnique();

            entity.ToTable("reading_books");
        });

        modelBuilder.Entity("Zero72.Blog.Reading.ReadingRecordEntity", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uuid");

            entity.Property<Guid>("BookId")
                .HasColumnType("uuid");

            entity.Property<string>("Chapter")
                .IsRequired()
                .HasMaxLength(300)
                .HasColumnType("character varying(300)");

            entity.Property<decimal>("DurationHours")
                .HasPrecision(4, 1)
                .HasColumnType("numeric(4,1)");

            entity.Property<TimeOnly>("FinishedAt")
                .HasColumnType("time without time zone");

            entity.Property<DateOnly>("ReadDate")
                .HasColumnType("date");

            entity.Property<string[]>("Reflections")
                .IsRequired()
                .HasColumnType("text[]");

            entity.Property<TimeOnly>("StartedAt")
                .HasColumnType("time without time zone");

            entity.HasKey("Id");

            entity.HasIndex("BookId");

            entity.HasIndex("ReadDate");

            entity.ToTable("reading_records");
        });

        modelBuilder.Entity("Zero72.Blog.Reading.ReadingRecordEntity", entity =>
        {
            entity.HasOne("Zero72.Blog.Reading.ReadingBookEntity", "Book")
                .WithMany("Records")
                .HasForeignKey("BookId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            entity.Navigation("Book");
        });

        modelBuilder.Entity("Zero72.Blog.Reading.ReadingBookEntity", entity =>
        {
            entity.Navigation("Records");
        });
    }
}
