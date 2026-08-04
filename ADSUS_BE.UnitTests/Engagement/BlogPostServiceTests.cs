using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Interfaces;
using ADSUS_BE.BLL.Engagement.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Moq;

namespace ADSUS_BE.UnitTests.Engagement;

/// <summary>
/// Tests cho BlogPostService (PUBLIC endpoints). GB-05: chỉ Published mới trả về cho
/// bệnh nhân. GB-01: trả 404 nếu Draft hoặc không tồn tại.
/// </summary>
public class BlogPostServiceTests
{
    private static BlogPost NewBlogPost(Guid? id = null, BlogPostStatus status = BlogPostStatus.Published, DateTime? publishedAt = null)
        => new()
        {
            PostId = id ?? Guid.NewGuid(),
            AuthorId = Guid.NewGuid(),
            Status = status,
            Title = "Test Title",
            Content = "Test Content",
            PublishedAt = publishedAt ?? (status == BlogPostStatus.Published ? DateTime.UtcNow : null),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    [Fact]
    public async Task ListPublishedAsync_ReturnsOnlyPublishedPosts()
    {
        // Arrange
        var posts = new List<BlogPost>
        {
            NewBlogPost(status: BlogPostStatus.Draft),
            NewBlogPost(status: BlogPostStatus.Published),
        };
        var repo = new Mock<IBlogPostRepository>();
        repo.Setup(r => r.ListPublishedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(posts.Where(p => p.Status == BlogPostStatus.Published).ToList());

        var sut = new BlogPostService(repo.Object);

        // Act
        var result = await sut.ListPublishedAsync();

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("Test Title", result.Items[0].Title);
    }

    [Fact]
    public async Task ListPublishedAsync_ReturnsEmptyPagedResult()
    {
        var repo = new Mock<IBlogPostRepository>();
        repo.Setup(r => r.ListPublishedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlogPost>());

        var sut = new BlogPostService(repo.Object);

        var result = await sut.ListPublishedAsync();

        Assert.Empty(result.Items);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsPost_WhenPublished()
    {
        var post = NewBlogPost(status: BlogPostStatus.Published);
        var repo = new Mock<IBlogPostRepository>();
        repo.Setup(r => r.GetByIdAsync(post.PostId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var sut = new BlogPostService(repo.Object);

        var result = await sut.GetByIdAsync(post.PostId);

        Assert.NotNull(result);
        Assert.Equal(post.PostId, result.Id);
        Assert.Equal("Test Title", result.Title);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenDraft()
    {
        var draft = NewBlogPost(status: BlogPostStatus.Draft);
        var repo = new Mock<IBlogPostRepository>();
        repo.Setup(r => r.GetByIdAsync(draft.PostId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);

        var sut = new BlogPostService(repo.Object);

        // Act — service lọc Draft, trả null cho bệnh nhân
        var result = await sut.GetByIdAsync(draft.PostId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var repo = new Mock<IBlogPostRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BlogPost?)null);

        var sut = new BlogPostService(repo.Object);

        var result = await sut.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    // ==================== Admin tests ====================

    [Fact]
    public async Task ListAllAsync_ReturnsAllPostsForAdmin()
    {
        var posts = new List<BlogPost>
        {
            NewBlogPost(status: BlogPostStatus.Draft),
            NewBlogPost(status: BlogPostStatus.Published),
        };
        var repo = new Mock<IBlogPostRepository>();
        repo.Setup(r => r.ListAllAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(posts);

        var sut = new BlogPostService(repo.Object);

        var result = await sut.ListAllAsync();

        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task ListAllAsync_FiltersByStatus()
    {
        var posts = new List<BlogPost>
        {
            NewBlogPost(status: BlogPostStatus.Draft),
            NewBlogPost(status: BlogPostStatus.Published),
        };
        var repo = new Mock<IBlogPostRepository>();
        repo.Setup(r => r.ListAllAsync(BlogPostStatus.Draft, It.IsAny<CancellationToken>()))
            .ReturnsAsync(posts.Where(p => p.Status == BlogPostStatus.Draft).ToList());

        var sut = new BlogPostService(repo.Object);

        var result = await sut.ListAllAsync(statusFilter: BlogPostStatus.Draft);

        Assert.Single(result.Items);
        Assert.Equal(BlogPostStatus.Draft, result.Items[0].Status);
    }

    [Fact]
    public async Task GetByIdForAdminAsync_ReturnsPost_EvenIfDraft()
    {
        var draft = NewBlogPost(status: BlogPostStatus.Draft);
        var repo = new Mock<IBlogPostRepository>();
        repo.Setup(r => r.GetByIdAsync(draft.PostId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);

        var sut = new BlogPostService(repo.Object);

        var result = await sut.GetByIdForAdminAsync(draft.PostId);

        Assert.NotNull(result);
        Assert.Equal(draft.PostId, result.Id);
        Assert.Equal(BlogPostStatus.Draft, result.Status);
    }

    [Fact]
    public async Task CreateAsync_CreatesDraftPost()
    {
        var repo = new Mock<IBlogPostRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<BlogPost>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BlogPost p, CancellationToken _) => p);

        var sut = new BlogPostService(repo.Object);
        var request = new CreateBlogPostRequest { Title = "New Post", Content = "Content" };
        var authorId = Guid.NewGuid();

        var result = await sut.CreateAsync(request, authorId);

        Assert.Equal("New Post", result.Title);
        Assert.Equal(BlogPostStatus.Draft, result.Status);
        repo.Verify(r => r.AddAsync(It.IsAny<BlogPost>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenPublished()
    {
        var published = NewBlogPost(status: BlogPostStatus.Published);
        var repo = new Mock<IBlogPostRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(published.PostId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(published);

        var sut = new BlogPostService(repo.Object);
        var request = new UpdateBlogPostRequest { Title = "Updated", Content = "Content" };

        var result = await sut.UpdateAsync(published.PostId, request);

        Assert.Null(result);
    }

    [Fact]
    public async Task PublishAsync_ReturnsNull_WhenAlreadyPublished()
    {
        var published = NewBlogPost(status: BlogPostStatus.Published);
        var repo = new Mock<IBlogPostRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(published.PostId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(published);

        var sut = new BlogPostService(repo.Object);

        var result = await sut.PublishAsync(published.PostId);

        Assert.Null(result);
    }

    [Fact]
    public async Task PublishAsync_SetsPublishedAt_WhenDraft()
    {
        var draft = NewBlogPost(status: BlogPostStatus.Draft);
        var repo = new Mock<IBlogPostRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(draft.PostId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);
        repo.Setup(r => r.UpdateAsync(It.IsAny<BlogPost>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new BlogPostService(repo.Object);

        var result = await sut.PublishAsync(draft.PostId);

        Assert.NotNull(result);
        Assert.Equal(BlogPostStatus.Published, result.Status);
        Assert.NotNull(result.PublishedAt);
        repo.Verify(r => r.UpdateAsync(It.IsAny<BlogPost>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
