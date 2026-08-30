using ADSUS_BE.BLL.Engagement.Services;
using Xunit;

namespace ADSUS_BE.UnitTests.Engagement;

/// <summary>
/// Tests cho ChatIntentDetector — Phase 2: chỉ query data sources cần thiết.
/// TDD: RED (compile fail) → GREEN (implement) per CLAUDE.md testing.md.
/// </summary>
public class ChatIntentDetectorTests
{
    private readonly ChatIntentDetector _sut = new();

    // ── Greeting ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Xin chào")]
    [InlineData("xin chào bạn")]
    [InlineData("Chào bác sĩ")]
    [InlineData("hello")]
    [InlineData("hi there")]
    public async Task Detect_Greeting_ReturnsGreeting(string message)
    {
        var result = await _sut.DetectAsync(message);

        Assert.Equal(ChatIntent.Greeting, result.Intent);
        Assert.Equal(DataSource.None, result.TriggeredSources);
    }

    // ── Prescription / Medication ─────────────────────────────────────────────

    [Theory]
    [InlineData("Thuốc của tôi uống như thế nào?")]
    [InlineData("đơn thuốc")]
    [InlineData("tôi quên uống thuốc")]
    [InlineData("liều lượng thuốc")]
    [InlineData("cách uống thuốc")]
    [InlineData("thuốc hôm nay")]
    [InlineData("uống thuốc trước hay sau ăn?")]
    [InlineData("lich uong thuoc hom nay cua toi la gi")]
    [InlineData("lịch uống thuốc hôm nay của tôi là gì")]
    public async Task Detect_PrescriptionKeywords_ReturnsPrescription(string message)
    {
        var result = await _sut.DetectAsync(message);

        Assert.Equal(ChatIntent.Prescription, result.Intent);
        Assert.True(result.TriggeredSources.HasFlag(DataSource.ActivePrescriptions));
        Assert.True(result.TriggeredSources.HasFlag(DataSource.TodayIntakes));
        Assert.False(result.TriggeredSources.HasFlag(DataSource.UpcomingAppointments));
    }

    // ── Appointment ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Lịch hẹn khám của tôi")]
    [InlineData("tôi có lịch khám không?")]
    [InlineData("đặt lịch khám")]
    [InlineData("khi nào được khám?")]
    [InlineData("gặp bác sĩ")]
    public async Task Detect_AppointmentKeywords_ReturnsAppointment(string message)
    {
        var result = await _sut.DetectAsync(message);

        Assert.Equal(ChatIntent.Appointment, result.Intent);
        Assert.True(result.TriggeredSources.HasFlag(DataSource.UpcomingAppointments));
        Assert.False(result.TriggeredSources.HasFlag(DataSource.ActivePrescriptions));
    }

    // ── Case / Diagnosis ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("Kết quả khám gần nhất")]
    [InlineData("chẩn đoán của tôi")]
    [InlineData("bác sĩ kết luận gì?")]
    [InlineData("xem lịch sử khám")]
    public async Task Detect_CaseKeywords_ReturnsCase(string message)
    {
        var result = await _sut.DetectAsync(message);

        Assert.Equal(ChatIntent.CaseHistory, result.Intent);
        Assert.True(result.TriggeredSources.HasFlag(DataSource.RecentCases));
        Assert.True(result.TriggeredSources.HasFlag(DataSource.Allergies));
        Assert.True(result.TriggeredSources.HasFlag(DataSource.Diseases));
    }

    // ── Allergy ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Tôi bị dị ứng gì?")]
    [InlineData("dị ứng với thuốc gì?")]
    [InlineData("dị ứng của tôi")]
    [InlineData("tôi bị dị ứng penicillin")]
    public async Task Detect_AllergyKeywords_ReturnsAllergy(string message)
    {
        var result = await _sut.DetectAsync(message);

        Assert.Equal(ChatIntent.Allergy, result.Intent);
        Assert.True(result.TriggeredSources.HasFlag(DataSource.Allergies));
    }

    // ── Disease ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Bệnh nền của tôi")]
    [InlineData("bệnh mãn tính")]
    [InlineData("tôi mắc bệnh gì?")]
    public async Task Detect_DiseaseKeywords_ReturnsDisease(string message)
    {
        var result = await _sut.DetectAsync(message);

        Assert.Equal(ChatIntent.Disease, result.Intent);
        Assert.True(result.TriggeredSources.HasFlag(DataSource.Diseases));
    }

    // ── Health log ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Nhật ký sức khỏe gần đây")]
    [InlineData("tôi đã ghi triệu chứng")]
    [InlineData("theo dõi sức khỏe")]
    [InlineData("hôm qua tôi bị đau đầu")]
    public async Task Detect_HealthLogKeywords_ReturnsHealthLog(string message)
    {
        var result = await _sut.DetectAsync(message);

        Assert.Equal(ChatIntent.HealthLog, result.Intent);
        Assert.True(result.TriggeredSources.HasFlag(DataSource.RecentHealthLogs));
    }

    // ── Blog / Health info ───────────────────────────────────────────────────

    [Theory]
    [InlineData("bài viết sức khỏe")]
    [InlineData("cho tôi xem blog")]
    [InlineData("tôi muốn đọc bài viết")]
    [InlineData("kiến thức sức khỏe")]
    public async Task Detect_BlogKeywords_ReturnsBlog(string message)
    {
        var result = await _sut.DetectAsync(message);

        Assert.Equal(ChatIntent.Blog, result.Intent);
        Assert.True(result.TriggeredSources.HasFlag(DataSource.RecentBlogs));
    }

    // ── General (fallback) ───────────────────────────────────────────────────

    [Theory]
    [InlineData("ADSUS là gì?")]
    [InlineData("hôm nay tôi khỏe không?")]
    [InlineData("nên ăn gì?")]
    [InlineData("tập thể dục có tốt không?")]
    [InlineData("cảm ơn bạn")]
    public async Task Detect_GeneralTopic_ReturnsGeneral(string message)
    {
        var result = await _sut.DetectAsync(message);

        Assert.Equal(ChatIntent.General, result.Intent);
        Assert.Equal(DataSource.None, result.TriggeredSources);
    }

    // ── Null / whitespace ───────────────────────────────────────────────────

    [Fact]
    public async Task Detect_Null_ReturnsGeneral()
    {
        var result = await _sut.DetectAsync(null!);

        Assert.Equal(ChatIntent.General, result.Intent);
    }

    [Fact]
    public async Task Detect_Whitespace_ReturnsGeneral()
    {
        var result = await _sut.DetectAsync("   ");

        Assert.Equal(ChatIntent.General, result.Intent);
    }
}
