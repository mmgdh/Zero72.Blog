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
    }
}
