using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.UnitTests.Engagement;

/// <summary>
/// Tests cho BlogPostRepository. Conventions:
/// - BlogPost.Status là Postgres enum blog_status (DRAFT/PUBLISHED)
/// - PublishedAt IS NULL khi còn Draft, NOT NULL khi Published
/// - Bệnh nhân chỉ thấy Published (GB-05)
/// - GB-01: Draft → Published một chiều (repository không enforce, handler enforce)
/// </summary>
public class BlogPostRepositoryTests
{
    private static AppDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    private static User NewAdmin(string phone = "0911111111")
        => new()
        {
            UserId = Guid.NewGuid(),
            Phone = phone,
            FullName = "Admin Test",
            PasswordHash = "x",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    private static BlogPost NewBlogPost(Guid authorId, BlogPostStatus status, DateTime? publishedAt = null)
        => new()
        {
            PostId = Guid.NewGuid(),
            AuthorId = authorId,
            Status = status,
            Title = $"Test Post {Guid.NewGuid():N}",
            Content = "Test content",
            PublishedAt = publishedAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    // === Failing tests — repository chưa tồn tại ===

    [Fact]
    public async Task ListPublishedAsync_ReturnsOnlyPublishedPosts()
    {
        // Arrange
        using var ctx = CreateContext();
        var admin = NewAdmin();
        ctx.Users.Add(admin);
        var draft = NewBlogPost(admin.UserId, BlogPostStatus.Draft);
        var published = NewBlogPost(admin.UserId, BlogPostStatus.Published, DateTime.UtcNow);
        ctx.BlogPosts.AddRange(draft, published);
        await ctx.SaveChangesAsync();

        var sut = new BlogPostRepository(ctx);

        // Act
        var result = await sut.ListPublishedAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(published.PostId, result[0].PostId);
    }

    [Fact]
    public async Task ListPublishedAsync_ReturnsEmptyWhenNoPublished()
    {
        // Arrange
        using var ctx = CreateContext();
        var admin = NewAdmin();
        ctx.Users.Add(admin);
        ctx.BlogPosts.Add(NewBlogPost(admin.UserId, BlogPostStatus.Draft));
        await ctx.SaveChangesAsync();

        var sut = new BlogPostRepository(ctx);

        // Act
        var result = await sut.ListPublishedAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task ListPublishedAsync_SortsByPublishedAtDescending()
    {
        // Arrange
        using var ctx = CreateContext();
        var admin = NewAdmin();
        ctx.Users.Add(admin);
        var older = NewBlogPost(admin.UserId, BlogPostStatus.Published, DateTime.UtcNow.AddDays(-2));
        var newer = NewBlogPost(admin.UserId, BlogPostStatus.Published, DateTime.UtcNow);
        ctx.BlogPosts.AddRange(older, newer);
        await ctx.SaveChangesAsync();

        var sut = new BlogPostRepository(ctx);

        // Act
        var result = await sut.ListPublishedAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.True(result[0].PublishedAt >= result[1].PublishedAt);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsPostWithAuthor()
    {
        // Arrange
        using var ctx = CreateContext();
        var admin = NewAdmin("0999999999");
        ctx.Users.Add(admin);
        var post = NewBlogPost(admin.UserId, BlogPostStatus.Published, DateTime.UtcNow);
        ctx.BlogPosts.Add(post);
        await ctx.SaveChangesAsync();

        var sut = new BlogPostRepository(ctx);

        // Act
        var result = await sut.GetByIdAsync(post.PostId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(post.PostId, result.PostId);
        Assert.NotNull(result.Author);
        Assert.Equal(admin.UserId, result.AuthorId);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        using var ctx = CreateContext();
        var sut = new BlogPostRepository(ctx);

        var result = await sut.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task ListAllAsync_IncludesBothDraftAndPublished()
    {
        // Arrange
        using var ctx = CreateContext();
        var admin = NewAdmin();
        ctx.Users.Add(admin);
        var draft = NewBlogPost(admin.UserId, BlogPostStatus.Draft);
        var published = NewBlogPost(admin.UserId, BlogPostStatus.Published, DateTime.UtcNow);
        ctx.BlogPosts.AddRange(draft, published);
        await ctx.SaveChangesAsync();

        var sut = new BlogPostRepository(ctx);

        // Act
        var result = await sut.ListAllAsync();

        // Assert
        Assert.Equal(2, result.Count);
    }
}
