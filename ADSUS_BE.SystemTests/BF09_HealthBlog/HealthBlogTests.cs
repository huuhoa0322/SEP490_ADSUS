using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Engagement.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ADSUS_BE.SystemTests.BF09_HealthBlog;

/// <summary>
/// Report 5.3 — BF-09 Health Blog Publishing & Consumption (HTTP Flow).
///
/// KHÔNG tráo bất kỳ service nào — request đi xuyên suốt qua tầng DAL thật vào DB test thật.
/// Chỉ chọn các test case cốt lõi. Không có endpoint "unpublish" — Draft -> Published là một
/// chiều theo đúng thiết kế (GB-01), nên STC004/STC005 là 2 cách duy nhất truy cập HTTP có thể
/// đụng vào ràng buộc một chiều này (thử publish lại, thử sửa bài đã publish).
///
/// Cần sẵn 1 tài khoản Admin đã seed thủ công qua SQL (giống BF-01).
/// </summary>
public class HealthBlogTests
{
    private const string SeedAdminPhone = "0900000001";
    private const string SeedAdminPassword = "Aa123456@";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task STC001_AdminCreatesAnArticle_StartsAsDraft()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);

        var response = await admin.PostAsJsonAsync("/api/v1/admin/blog-posts", new
        {
            title = "STC001 A new health article",
            content = "STC001 content body",
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AdminBlogPostDetailResponse>>(JsonOptions, ct);
        Assert.Equal("Draft", body!.Data!.Status.ToString());
        Assert.Null(body.Data.PublishedAt);
    }

    [Fact]
    public async Task STC002_PublishingADraftArticle_MakesItAppearThroughPublicAccess()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var postId = await CreateDraftAsync(admin, "STC002", ct);

        var publishResponse = await admin.PostAsync($"/api/v1/admin/blog-posts/{postId}/publish", null, ct);
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
        var published = await publishResponse.Content.ReadFromJsonAsync<ApiResponse<AdminBlogPostDetailResponse>>(JsonOptions, ct);
        Assert.Equal("Published", published!.Data!.Status.ToString());
        Assert.NotNull(published.Data.PublishedAt);

        var guest = app.CreateClient();
        var detailResponse = await guest.GetAsync($"/api/v1/blog-posts/{postId}", ct);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<ApiResponse<BlogPostDetailResponse>>(JsonOptions, ct);
        Assert.Equal("STC002 Article", detail!.Data!.Title);
    }

    [Fact]
    public async Task STC003_ADraftArticle_IsNotVisibleThroughPublicBlogAccess()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var postId = await CreateDraftAsync(admin, "STC003", ct);
        var guest = app.CreateClient();

        var detailResponse = await guest.GetAsync($"/api/v1/blog-posts/{postId}", ct);
        Assert.Equal(HttpStatusCode.NotFound, detailResponse.StatusCode);

        var listResponse = await guest.GetAsync("/api/v1/blog-posts?pageSize=50", ct);
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<BlogPostListItemResponse>>>(JsonOptions, ct);
        Assert.DoesNotContain(list!.Data!.Items, p => p.Id == postId);
    }

    [Fact]
    public async Task STC004_PublishingAnAlreadyPublishedArticleAgain_IsRejected()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var postId = await CreateDraftAsync(admin, "STC004", ct);
        (await admin.PostAsync($"/api/v1/admin/blog-posts/{postId}/publish", null, ct)).EnsureSuccessStatusCode();

        var secondPublishResponse = await admin.PostAsync($"/api/v1/admin/blog-posts/{postId}/publish", null, ct);

        Assert.Equal(HttpStatusCode.BadRequest, secondPublishResponse.StatusCode);
    }

    [Fact]
    public async Task STC005_EditingAPublishedArticle_IsRejected()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var postId = await CreateDraftAsync(admin, "STC005", ct);
        (await admin.PostAsync($"/api/v1/admin/blog-posts/{postId}/publish", null, ct)).EnsureSuccessStatusCode();

        var updateResponse = await admin.PutAsJsonAsync($"/api/v1/admin/blog-posts/{postId}", new
        {
            title = "STC005 Attempted edit after publish",
            content = "STC005 should not be allowed",
        }, ct);

        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);
    }

    [Fact]
    public async Task STC006_AnUnauthenticatedGuest_CanBrowseAndReadAPublishedArticle()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var postId = await CreateDraftAsync(admin, "STC006", ct);
        (await admin.PostAsync($"/api/v1/admin/blog-posts/{postId}/publish", null, ct)).EnsureSuccessStatusCode();

        var guest = app.CreateClient(); // Không gắn Authorization header — mô phỏng Guest.

        var listResponse = await guest.GetAsync("/api/v1/blog-posts?pageSize=50", ct);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<BlogPostListItemResponse>>>(JsonOptions, ct);
        Assert.Contains(list!.Data!.Items, p => p.Id == postId);

        var detailResponse = await guest.GetAsync($"/api/v1/blog-posts/{postId}", ct);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
    }

    // ---- shared setup ----

    private static async Task<Guid> CreateDraftAsync(HttpClient admin, string label, CancellationToken ct)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/admin/blog-posts", new
        {
            title = $"{label} Article",
            content = $"{label} content body",
        }, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AdminBlogPostDetailResponse>>(JsonOptions, ct);
        return body!.Data!.Id;
    }

    // ---- helpers ----

    private static WebApplicationFactory<Program> CreateApp() => new();

    private static async Task<HttpClient> LoginAsAdminAsync(WebApplicationFactory<Program> app)
    {
        var ct = TestContext.Current.CancellationToken;
        var loginResponse = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            phoneNumber = SeedAdminPhone,
            password = SeedAdminPassword,
        }, ct);
        loginResponse.EnsureSuccessStatusCode();
        var body = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions, ct);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
        return client;
    }
}
