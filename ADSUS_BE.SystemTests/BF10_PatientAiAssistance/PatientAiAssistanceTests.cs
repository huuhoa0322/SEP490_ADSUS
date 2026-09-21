using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ADSUS_BE.SystemTests.BF10_PatientAiAssistance;

/// <summary>
/// Report 5.3 — BF-10 Patient AI Assistance (HTTP Flow).
///
/// KHÔNG tráo bất kỳ service nào — request đi xuyên suốt qua tầng DAL thật vào DB test thật.
/// Chỉ chọn các test case cốt lõi.
///
/// LỆCH SO VỚI TÀI LIỆU: Report 3 nói hệ thống cảnh báo sau 10 lượt hỏi và chặn hẳn sau 15 lượt
/// trong 5 giờ. Đọc thẳng ChatRateLimitConstants xác nhận số thật khác hoàn toàn:
/// WarningThreshold=40, MaxCallsPerWindow=50, RateLimitWindow=1 giờ. Test bám theo số thật.
///
/// STC001 là case DUY NHẤT gọi thật sang Google Gemini API (IChatClient đăng ký
/// GeminiChatClient trong Program.cs, không phải FakeChatClient) — phụ thuộc GeminiApiKey đã
/// cấu hình qua user-secrets, giống AI Python Backend ở BF-02. Các case còn lại tự chặn trước
/// khi gọi LLM (psychology filter / rate limit) nên không phụ thuộc dependency ngoài này.
///
/// Để tái hiện việc chạm ngưỡng rate limit (50 lượt trong 1 giờ) mà không cần gọi LLM thật 50
/// lần, STC004 chèn thẳng 50 dòng AiChatMessage (Role=Assistant) qua AppDbContext của app —
/// tương đương seed dữ liệu setup, không phải mock logic đang kiểm thử.
///
/// Cần sẵn 1 tài khoản Admin đã seed thủ công qua SQL (giống BF-01).
/// </summary>
public class PatientAiAssistanceTests
{
    private const string SeedAdminPhone = "0900000001";
    private const string SeedAdminPassword = "Aa123456@";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task STC001_PatientAsksAPermittedQuestion_ReceivesAnAiGeneratedResponse()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var (patient, _) = await CreatePatientAsync(app, admin, "STC001 Patient", ct);

        var response = await SendChatMessageAsync(patient, "Uống đủ nước mỗi ngày có lợi ích gì?", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ChatMessageResponse>>(JsonOptions, ct);
        Assert.False(body!.Data!.IsSafetyResponse);
        Assert.False(body.Data.IsRateLimitExceeded);
        Assert.False(string.IsNullOrWhiteSpace(body.Data.Content));
    }

    [Fact]
    public async Task STC002_ABlockedPsychologyTopic_ReceivesTheSafetyResponse_WithoutCallingTheLlm()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var (patient, _) = await CreatePatientAsync(app, admin, "STC002 Patient", ct);

        var response = await SendChatMessageAsync(patient, "Gần đây tôi cảm thấy trầm cảm quá", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ChatMessageResponse>>(JsonOptions, ct);
        Assert.True(body!.Data!.IsSafetyResponse);
        Assert.Null(body.Data.DetectedIntent);
        Assert.Contains("không hỗ trợ tư vấn tâm lý", body.Data.Content);
    }

    [Fact]
    public async Task STC003_SendingAnEmptyMessage_IsRejected()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var (patient, _) = await CreatePatientAsync(app, admin, "STC003 Patient", ct);

        var response = await SendChatMessageAsync(patient, "   ", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task STC004_AfterReachingTheMaxCallsPerWindow_AFurtherQuestionSkipsTheLlm()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var (patient, userId) = await CreatePatientAsync(app, admin, "STC004 Patient", ct);
        await SeedAssistantMessagesAsync(app, userId, ChatRateLimitConstants.MaxCallsPerWindow, ct);

        var response = await SendChatMessageAsync(patient, "Tập thể dục buổi sáng có tốt không?", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ChatMessageResponse>>(JsonOptions, ct);
        Assert.True(body!.Data!.IsRateLimitExceeded);
        Assert.False(body.Data.IsSafetyResponse);
        Assert.Contains($"{ChatRateLimitConstants.MaxCallsPerWindow} lượt hỏi", body.Data.Content);
    }

    [Fact]
    public async Task STC005_PatientReviewsOwnHistory_WithinADateRange_MissingFromOrToIsRejected()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var (patient, _) = await CreatePatientAsync(app, admin, "STC005 Patient", ct);
        (await SendChatMessageAsync(patient, "Gần đây tôi cảm thấy trầm cảm quá", ct)).EnsureSuccessStatusCode();

        var from = DateTime.UtcNow.AddDays(-1).ToString("O");
        var to = DateTime.UtcNow.AddDays(1).ToString("O");

        var historyResponse = await patient.GetAsync($"/api/v1/me/chat/messages?from={from}&to={to}", ct);
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = await historyResponse.Content.ReadFromJsonAsync<ApiResponse<ChatHistoryResponse>>(JsonOptions, ct);
        Assert.Contains(history!.Data!.Messages, m => m.IsSafetyResponse);

        var missingRangeResponse = await patient.GetAsync("/api/v1/me/chat/messages", ct);
        Assert.Equal(HttpStatusCode.BadRequest, missingRangeResponse.StatusCode);
    }

    [Fact]
    public async Task STC006_APatientsConversationHistory_NeverIncludesAnotherPatientsMessages()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var (patientA, _) = await CreatePatientAsync(app, admin, "STC006 Patient A", ct);
        var (patientB, _) = await CreatePatientAsync(app, admin, "STC006 Patient B", ct);
        (await SendChatMessageAsync(patientA, "Gần đây tôi cảm thấy trầm cảm quá", ct)).EnsureSuccessStatusCode();

        var from = DateTime.UtcNow.AddDays(-1).ToString("O");
        var to = DateTime.UtcNow.AddDays(1).ToString("O");
        var historyResponse = await patientB.GetAsync($"/api/v1/me/chat/messages?from={from}&to={to}", ct);
        historyResponse.EnsureSuccessStatusCode();
        var history = await historyResponse.Content.ReadFromJsonAsync<ApiResponse<ChatHistoryResponse>>(JsonOptions, ct);

        Assert.Empty(history!.Data!.Messages);
    }

    // ---- shared setup ----

    private static Task<HttpResponseMessage> SendChatMessageAsync(HttpClient patient, string content, CancellationToken ct) =>
        patient.PostAsJsonAsync("/api/v1/me/chat/messages", new { content }, ct);

    /// <summary>Chèn thẳng N dòng AiChatMessage (Role=Assistant, CreatedAt=now) qua AppDbContext
    /// của app — tương đương backdate/seed dữ liệu setup, cùng DB test thật, không mock service
    /// đang được kiểm thử.</summary>
    private static async Task SeedAssistantMessagesAsync(
        WebApplicationFactory<Program> app, Guid userId, int count, CancellationToken ct)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var messages = Enumerable.Range(0, count).Select(_ => new AiChatMessage
        {
            MessageId = Guid.NewGuid(),
            UserId = userId,
            Content = "Seeded assistant reply for rate-limit setup.",
            Role = ChatRole.Assistant,
            CreatedAt = now,
        });
        await db.AiChatMessages.AddRangeAsync(messages, ct);
        await db.SaveChangesAsync(ct);
    }

    // ---- helpers ----

    private static WebApplicationFactory<Program> CreateApp() => new();

    private static string UniquePhone() => "09" + Random.Shared.Next(10_000_000, 99_999_999);

    private static async Task<HttpClient> LoginAsAdminAsync(WebApplicationFactory<Program> app) =>
        await LoginAndAuthorizeAsync(app, SeedAdminPhone, SeedAdminPassword, TestContext.Current.CancellationToken);

    private static async Task<HttpClient> LoginAndAuthorizeAsync(
        WebApplicationFactory<Program> app, string phone, string password, CancellationToken ct)
    {
        var loginResponse = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            phoneNumber = phone,
            password,
        }, ct);
        loginResponse.EnsureSuccessStatusCode();
        var body = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions, ct);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
        return client;
    }

    private const string FinalTestPassword = "Aa123456@";

    /// <summary>Đăng nhập bằng mật khẩu tạm do Admin cấp, rồi đổi ngay sang mật khẩu cố định.
    /// MustChangePasswordMiddleware chặn (403) mọi request khác ngoài change-password/logout khi
    /// access token còn mang claim MustChangePassword=true.</summary>
    private static async Task<HttpClient> LoginAndForcePasswordChangeAsync(
        WebApplicationFactory<Program> app, string phone, string temporaryPassword, CancellationToken ct)
    {
        var tempClient = await LoginAndAuthorizeAsync(app, phone, temporaryPassword, ct);
        var changeResponse = await tempClient.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            newPassword = FinalTestPassword,
            confirmNewPassword = FinalTestPassword,
        }, ct);
        changeResponse.EnsureSuccessStatusCode();
        var body = await changeResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions, ct);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
        return client;
    }

    /// <summary>Chat không cần PatientProfile (chỉ ChatMessagesController.[Authorize(Roles =
    /// "PATIENT")], không tra PatientProfileRepository như HealthLogsController) — chỉ cần tài
    /// khoản Patient thật, không cần dựng hồ sơ qua Doctor như các BF khác.</summary>
    private static async Task<(HttpClient Client, Guid UserId)> CreatePatientAsync(
        WebApplicationFactory<Program> app, HttpClient admin, string fullName, CancellationToken ct)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/admin/users", new
        {
            phoneNumber = UniquePhone(),
            fullName,
            role = "PATIENT",
        }, ct);
        response.EnsureSuccessStatusCode();
        var created = (await response.Content.ReadFromJsonAsync<ApiResponse<CreatedUserAccountResponse>>(JsonOptions, ct))!.Data!;

        var client = await LoginAndForcePasswordChangeAsync(app, created.Account.PhoneNumber, created.TemporaryPassword, ct);
        return (client, created.Account.UserId);
    }
}
