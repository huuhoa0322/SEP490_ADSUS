using ADSUS_BE.BLL.DashboardReporting.Services;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.DashboardReporting;

/// <summary>
/// UC-05 FT-10 — thống kê vận hành hệ thống (SCR-08).
///
/// Trọng tâm kiểm thử là AF-01: màn này KHÔNG BAO GIỜ được vỡ. Khoảng thời gian trống, dữ
/// liệu trống, ngày nhập ngược, ngày sai định dạng — tất cả đều phải ra số 0 hoặc một khoảng
/// hợp lệ, không được ném lỗi.
/// </summary>
public class DashboardServiceTests
{
    private readonly Mock<IDashboardRepository> _repo = new();
    private readonly DashboardService _sut;

    private DateTime _capturedFrom;
    private DateTime _capturedTo;

    public DashboardServiceTests()
    {
        _repo.Setup(r => r.GetAccountCountsAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(new AccountCounts(0, 0, 0, 0, 0, 0, 0, 0));

        _repo.Setup(r => r.GetActivityCountsAsync(
                 It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
             .Callback<DateTime, DateTime, CancellationToken>((f, t, _) =>
             {
                 _capturedFrom = f;
                 _capturedTo = t;
             })
             .ReturnsAsync(KhongCoHoatDong());

        _repo.Setup(r => r.GetDailyActivityAsync(
                 It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(Array.Empty<DailyActivity>());

        _sut = new DashboardService(_repo.Object);
    }

    // ---------- AF-01: không bao giờ vỡ ----------

    [Fact]
    public async Task KhongCoDuLieu_TraVeToanSo0_KhongNemLoi()
    {
        var result = await _sut.GetStatisticsAsync(null, null);

        Assert.Equal(0, result.Accounts.Total);
        Assert.Equal(0, result.Accounts.ActiveRate);
        Assert.Equal(0, result.Clinical.CaseCount);
        Assert.Equal(0, result.Clinical.AiConfirmRate);
        Assert.Equal(0, result.Appointments.CancellationRate);
        Assert.Equal(0, result.Appointments.AverageBookingsPerSlot);
        Assert.Equal(0, result.Adherence.AdherenceRate);
    }

    [Theory]
    [InlineData("khong-phai-ngay", null)]
    [InlineData("2026/07/31", "31-07-2026")]
    [InlineData("", "")]
    public async Task NgayThangSaiDinhDang_RoiVeMacDinh_KhongNemLoi(string? from, string? to)
    {
        // Bỏ qua giá trị sai và dùng mặc định, thay vì trả 400 làm màn hình trắng xoá.
        var result = await _sut.GetStatisticsAsync(from, to);

        Assert.False(string.IsNullOrEmpty(result.FromDate));
        Assert.False(string.IsNullOrEmpty(result.ToDate));
    }

    // ---------- Khoảng thời gian ----------

    [Fact]
    public async Task KhongChonGi_LayMacDinh30NgayGanNhat()
    {
        var result = await _sut.GetStatisticsAsync(null, null);

        var from = DateOnly.Parse(result.FromDate);
        var to = DateOnly.Parse(result.ToDate);

        Assert.Equal(30, to.DayNumber - from.DayNumber);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), to);
    }

    [Fact]
    public async Task ChonNguocNgay_TuDongDoiCho()
    {
        // Người dùng chọn "từ 31/07 đến 01/07" thì hiểu là họ chọn nhầm thứ tự, chứ không
        // phải muốn xem một khoảng rỗng.
        var result = await _sut.GetStatisticsAsync("2026-07-31", "2026-07-01");

        Assert.Equal("2026-07-01", result.FromDate);
        Assert.Equal("2026-07-31", result.ToDate);
    }

    [Fact]
    public async Task KhoangQuaDai_BiCatNganLai()
    {
        // Chặn một cú bấm nhầm quét cả bảng nhiều năm trên Supabase.
        var result = await _sut.GetStatisticsAsync("2000-01-01", "2026-07-31");

        var from = DateOnly.Parse(result.FromDate);
        var to = DateOnly.Parse(result.ToDate);

        Assert.Equal(366, to.DayNumber - from.DayNumber);
    }

    [Fact]
    public async Task NgayCuoi_DUOC_TINH_TRON_VEN()
    {
        // Mốc kết thúc phải là 00:00 của NGÀY HÔM SAU. Nếu truyền 00:00 của chính ngày cuối
        // thì mọi thứ phát sinh trong ngày hôm đó đều bị bỏ sót.
        await _sut.GetStatisticsAsync("2026-07-01", "2026-07-31");

        Assert.Equal(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), _capturedFrom);
        Assert.Equal(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), _capturedTo);
    }

    // ---------- Cách tính tỉ lệ ----------

    [Fact]
    public async Task TiLeTaiKhoanHoatDong_TinhDung()
    {
        _repo.Setup(r => r.GetAccountCountsAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(new AccountCounts(
                 Total: 10, AdminCount: 1, DoctorCount: 3, NurseCount: 2, PatientCount: 4,
                 ActiveCount: 8, LockedCount: 1, DeactivatedCount: 1));

        var result = await _sut.GetStatisticsAsync(null, null);

        Assert.Equal(80.0, result.Accounts.ActiveRate);
        Assert.Equal(2, result.Accounts.NurseCount);
    }

    [Fact]
    public async Task TiLeXacNhanAI_KHONG_TINH_PHAN_DANG_CHO_VAO_MAU_SO()
    {
        // 6 xác nhận, 2 từ chối, 92 đang chờ duyệt.
        // Đúng: 6/(6+2) = 75%. Sai: 6/100 = 6% — con số đó nói bác sĩ làm việc kém, trong khi
        // thực tế chỉ là có nhiều ca mới chưa kịp duyệt.
        SetupHoatDong(aiConfirmed: 6, aiRejected: 2, aiPending: 92, aiRun: 100);

        var result = await _sut.GetStatisticsAsync(null, null);

        Assert.Equal(75.0, result.Clinical.AiConfirmRate);
        Assert.Equal(92, result.Clinical.AiPendingCount);
    }

    [Fact]
    public async Task TiLeXacNhanAI_ChuaDuyetCaiNao_TraVe0_KhongChiaCho0()
    {
        SetupHoatDong(aiConfirmed: 0, aiRejected: 0, aiPending: 50, aiRun: 50);

        var result = await _sut.GetStatisticsAsync(null, null);

        Assert.Equal(0, result.Clinical.AiConfirmRate);
    }

    [Fact]
    public async Task TiLeHuyLichHen_TinhTrenTongSoLuotDat()
    {
        SetupHoatDong(booked: 30, cancelled: 10, slots: 20);

        var result = await _sut.GetStatisticsAsync(null, null);

        // 10 huỷ trên tổng 40 lượt đặt.
        Assert.Equal(25.0, result.Appointments.CancellationRate);
        // 40 lượt đặt trên 20 khung giờ.
        Assert.Equal(2.0, result.Appointments.AverageBookingsPerSlot);
    }

    [Fact]
    public async Task KhongCoKhungGioNao_TraVe0_KhongChiaCho0()
    {
        SetupHoatDong(booked: 5, cancelled: 0, slots: 0);

        var result = await _sut.GetStatisticsAsync(null, null);

        Assert.Equal(0, result.Appointments.AverageBookingsPerSlot);
    }

    [Fact]
    public async Task TiLeTuanThuUongThuoc_TinhDung()
    {
        SetupHoatDong(doses: 200, taken: 173);

        var result = await _sut.GetStatisticsAsync(null, null);

        Assert.Equal(86.5, result.Adherence.AdherenceRate);
    }

    // ---------- Biểu đồ xu hướng ----------

    [Fact]
    public async Task XuHuong_DU_MOI_NGAY_KE_CA_NGAY_KHONG_CO_GI()
    {
        // Repository chỉ trả về ngày CÓ phát sinh. Nếu đưa thẳng dãy thưa đó lên biểu đồ thì
        // đường nối thẳng qua các ngày trống, nhìn như hoạt động vẫn đều trong khi thực tế
        // là không có gì — đọc sai hẳn ý nghĩa.
        _repo.Setup(r => r.GetDailyActivityAsync(
                 It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<DailyActivity>
             {
                 new(new DateOnly(2026, 7, 3), NewAccounts: 2, Cases: 1, Appointments: 0),
             });

        var result = await _sut.GetStatisticsAsync("2026-07-01", "2026-07-05");

        // 1,2,3,4,5 tháng 7 — cả hai đầu đều được tính vào.
        Assert.Equal(5, result.Trend.Count);
        Assert.Equal("2026-07-01", result.Trend[0].Date);
        Assert.Equal("2026-07-05", result.Trend[4].Date);

        Assert.Equal(0, result.Trend[0].NewAccounts);
        Assert.Equal(2, result.Trend[2].NewAccounts);
        Assert.Equal(1, result.Trend[2].Cases);
        Assert.Equal(0, result.Trend[4].NewAccounts);
    }

    [Fact]
    public async Task XuHuong_KhongCoDuLieu_VanDuDiem_ToanSo0()
    {
        // AF-01 — khoảng trống vẫn phải vẽ được, không được ném lỗi hay trả dãy rỗng.
        var result = await _sut.GetStatisticsAsync("2026-07-01", "2026-07-03");

        Assert.Equal(3, result.Trend.Count);
        Assert.All(result.Trend, p =>
        {
            Assert.Equal(0, p.NewAccounts);
            Assert.Equal(0, p.Cases);
            Assert.Equal(0, p.Appointments);
        });
    }

    // ---------- helpers ----------

    private static ActivityCounts KhongCoHoatDong() =>
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private void SetupHoatDong(
        int aiConfirmed = 0,
        int aiRejected = 0,
        int aiPending = 0,
        int aiRun = 0,
        int booked = 0,
        int cancelled = 0,
        int slots = 0,
        int doses = 0,
        int taken = 0)
    {
        _repo.Setup(r => r.GetActivityCountsAsync(
                 It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new ActivityCounts(
                 NewAccounts: 0,
                 CaseCount: 0,
                 AiRunCount: aiRun,
                 AiConfirmedCount: aiConfirmed,
                 AiRejectedCount: aiRejected,
                 AiPendingCount: aiPending,
                 AppointmentBookedCount: booked,
                 AppointmentCancelledCount: cancelled,
                 ScheduleSlotCount: slots,
                 MedicationDoseCount: doses,
                 MedicationTakenCount: taken));
    }
}
