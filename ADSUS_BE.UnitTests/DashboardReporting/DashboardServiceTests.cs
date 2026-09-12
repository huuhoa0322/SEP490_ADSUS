using ADSUS_BE.BLL.DashboardReporting.DTOs;
using ADSUS_BE.BLL.DashboardReporting.Services;
using ADSUS_BE.DAL.Data;
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

    private DateOnly _capturedFrom;
    private DateOnly _capturedTo;

    public DashboardServiceTests()
    {
        _repo.Setup(r => r.GetAccountCountsAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(new AccountCounts(0, 0, 0, 0, 0, 0, 0));

        _repo.Setup(r => r.GetActivityCountsAsync(
                 It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
             .Callback<DateOnly, DateOnly, CancellationToken>((f, t, _) =>
             {
                 _capturedFrom = f;
                 _capturedTo = t;
             })
             .ReturnsAsync(NoActivity());

        _repo.Setup(r => r.GetDailyActivityAsync(
                 It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(Array.Empty<DailyActivity>());

        _repo.Setup(r => r.GetRevenueAsync(
                 It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new RevenueCounts(0m, 0, 0m, 0, 0m, 0, 0, 0m));

        _repo.Setup(r => r.GetTopPrescribedMedicinesAsync(
                 It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(Array.Empty<TopMedicine>());

        var aiModelRepo = new Mock<IAiModelVersionRepository>();
        _sut = new DashboardService(_repo.Object, aiModelRepo.Object);
    }

    // ---------- AF-01: không bao giờ vỡ ----------

    [Fact]
    public async Task NoData_ReturnsAllZeros_DoesNotThrow()
    {
        var result = await _sut.GetStatisticsAsync(null, null, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Accounts.Total);
        Assert.Equal(0, result.Accounts.ActiveRate);
        Assert.Equal(0, result.Clinical.CaseCount);
        Assert.Equal(0, result.Clinical.AiConfirmRate);
        Assert.Equal(0, result.Appointments.CancellationRate);
        Assert.Equal(0, result.Appointments.SlotCount);
        Assert.Equal(0, result.Adherence.AdherenceRate);
    }

    [Theory]
    [InlineData("khong-phai-ngay", null)]
    [InlineData("2026/07/31", "31-07-2026")]
    [InlineData("", "")]
    public async Task MalformedDates_FallsBackToDefault_DoesNotThrow(string? from, string? to)
    {
        // Bỏ qua giá trị sai và dùng mặc định, thay vì trả 400 làm màn hình trắng xoá.
        var result = await _sut.GetStatisticsAsync(from, to, TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrEmpty(result.FromDate));
        Assert.False(string.IsNullOrEmpty(result.ToDate));
    }

    // ---------- Khoảng thời gian ----------

    [Fact]
    public async Task NothingSelected_UsesDefaultLast30Days()
    {
        var result = await _sut.GetStatisticsAsync(null, null, TestContext.Current.CancellationToken);

        var from = DateOnly.Parse(result.FromDate);
        var to = DateOnly.Parse(result.ToDate);

        // Tính CẢ HAI ĐẦU: 30 ngày là 30 điểm trên biểu đồ, nên chênh lệch phải là 29.
        // Trước đây trừ thẳng 30 nên "30 ngày" ra 31 ngày — nhãn trên nút nói dối.
        Assert.Equal(29, to.DayNumber - from.DayNumber);

        // Hôm nay theo giờ Việt Nam, KHÔNG phải theo UTC. Từ 00:00 đến 07:00 giờ Việt Nam
        // hai mốc này lệch nhau đúng một ngày.
        Assert.Equal(ClinicClock.Today(), to);
    }

    [Fact]
    public async Task DateRange_PassedDownIntactBothEnds()
    {
        await _sut.GetStatisticsAsync("2026-07-01", "2026-07-31", TestContext.Current.CancellationToken);

        Assert.Equal(new DateOnly(2026, 7, 1), _capturedFrom);
        Assert.Equal(new DateOnly(2026, 7, 31), _capturedTo);
    }

    [Fact]
    public async Task ReversedDates_AutomaticallySwapped()
    {
        // Người dùng chọn "từ 31/07 đến 01/07" thì hiểu là họ chọn nhầm thứ tự, chứ không
        // phải muốn xem một khoảng rỗng.
        var result = await _sut.GetStatisticsAsync("2026-07-31", "2026-07-01", TestContext.Current.CancellationToken);

        Assert.Equal("2026-07-01", result.FromDate);
        Assert.Equal("2026-07-31", result.ToDate);
    }

    [Fact]
    public async Task RangeTooLong_IsClampedShorter()
    {
        // Chặn một cú bấm nhầm quét cả bảng nhiều năm trên Supabase.
        var result = await _sut.GetStatisticsAsync("2000-01-01", "2026-07-31", TestContext.Current.CancellationToken);

        var from = DateOnly.Parse(result.FromDate);
        var to = DateOnly.Parse(result.ToDate);

        // 366 điểm, tức chênh lệch 365 ngày — tính cả hai đầu.
        Assert.Equal(365, to.DayNumber - from.DayNumber);
    }

    [Fact]
    public void EndDate_IsFullyInclusive()
    {
        // Mốc kết thúc phải là đầu NGÀY HÔM SAU theo giờ phòng khám. Lấy đúng cuối ngày là
        // mọi thứ phát sinh trong ngày hôm đó đều bị bỏ sót, vì mọi mốc giờ đều lớn hơn 00:00.
        var date = new DateOnly(2026, 7, 31);

        // 01/08 ở Việt Nam bắt đầu lúc 17:00 ngày 31/07 giờ UTC.
        Assert.Equal(
            new DateTime(2026, 7, 31, 17, 0, 0, DateTimeKind.Utc),
            ClinicClock.EndOfDayExclusiveUtc(date));

        Assert.Equal(
            new DateTime(2026, 7, 30, 17, 0, 0, DateTimeKind.Utc),
            ClinicClock.StartOfDayUtc(date));
    }

    // ---------- Cách tính tỉ lệ ----------

    [Fact]
    public async Task ActiveAccountRate_CalculatedCorrectly()
    {
        _repo.Setup(r => r.GetAccountCountsAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(new AccountCounts(
                 Total: 10, AdminCount: 1, DoctorCount: 3, NurseCount: 2, PatientCount: 4,
                 ActiveCount: 8, DeactivatedCount: 1));

        var result = await _sut.GetStatisticsAsync(null, null, TestContext.Current.CancellationToken);

        Assert.Equal(80.0, result.Accounts.ActiveRate);
        Assert.Equal(2, result.Accounts.NurseCount);
    }

    [Fact]
    public async Task AiConfirmRate_ExcludesPendingFromDenominator()
    {
        // 6 xác nhận, 2 từ chối, 92 đang chờ duyệt.
        // Đúng: 6/(6+2) = 75%. Sai: 6/100 = 6% — con số đó nói bác sĩ làm việc kém, trong khi
        // thực tế chỉ là có nhiều ca mới chưa kịp duyệt.
        SetupActivity(aiConfirmed: 6, aiRejected: 2, aiPending: 92, aiRun: 100);

        var result = await _sut.GetStatisticsAsync(null, null, TestContext.Current.CancellationToken);

        Assert.Equal(75.0, result.Clinical.AiConfirmRate);
        Assert.Equal(92, result.Clinical.AiPendingCount);
    }

    [Fact]
    public async Task AiConfirmRate_NoneReviewedYet_ReturnsZeroWithoutDivideByZero()
    {
        SetupActivity(aiConfirmed: 0, aiRejected: 0, aiPending: 50, aiRun: 50);

        var result = await _sut.GetStatisticsAsync(null, null, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Clinical.AiConfirmRate);
    }

    [Fact]
    public async Task CancellationRate_CalculatedOverTotalBookings()
    {
        SetupActivity(booked: 30, cancelled: 10, slots: 20);

        var result = await _sut.GetStatisticsAsync(null, null, TestContext.Current.CancellationToken);

        // 10 huỷ trên tổng 40 lượt đặt.
        Assert.Equal(25.0, result.Appointments.CancellationRate);

        // Số khung giờ đã mở là con số đếm thuần, đưa nguyên xi lên màn hình.
        Assert.Equal(20, result.Appointments.SlotCount);
    }

    [Fact]
    public void NoSlotUtilizationRateMetric_SlotHasNoCapacityColumn()
    {
        // ScheduleSlot không có cột Capacity, và chính entity ghi rõ "không giới hạn số
        // Appointment/slot" (quyết định UCS 3.1 ngày 23/07/2026). Không có mẫu số thì không
        // có tỉ lệ lấp đầy, nên chỉ số đó đã bị bỏ hẳn thay vì thay bằng một con số khác
        // nghĩa mà người đọc dễ tưởng là tỉ lệ.
        //
        // Bài test này canh chừng việc ai đó thêm lại: nếu có ngày ScheduleSlot có Capacity
        // thật thì cứ xoá bài test này đi và tính tỉ lệ cho đúng.
        var propertyNames = typeof(AppointmentStatistics).GetProperties().Select(p => p.Name);

        Assert.DoesNotContain("AverageBookingsPerSlot", propertyNames);
        Assert.DoesNotContain("UtilizationRate", propertyNames);
    }

    [Fact]
    public async Task AdherenceRate_CalculatedCorrectly()
    {
        SetupActivity(doses: 200, taken: 173);

        var result = await _sut.GetStatisticsAsync(null, null, TestContext.Current.CancellationToken);

        Assert.Equal(86.5, result.Adherence.AdherenceRate);
    }

    // ---------- Biểu đồ xu hướng ----------

    [Fact]
    public async Task Trend_FillsEveryDayIncludingEmptyOnes()
    {
        // Repository chỉ trả về ngày CÓ phát sinh. Nếu đưa thẳng dãy thưa đó lên biểu đồ thì
        // đường nối thẳng qua các ngày trống, nhìn như hoạt động vẫn đều trong khi thực tế
        // là không có gì — đọc sai hẳn ý nghĩa.
        _repo.Setup(r => r.GetDailyActivityAsync(
                 It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<DailyActivity>
             {
                  new(new DateOnly(2026, 7, 3), NewAccounts: 2, Cases: 1, Appointments: 0, Revenue: 0m),
              });

        var result = await _sut.GetStatisticsAsync("2026-07-01", "2026-07-05", TestContext.Current.CancellationToken);

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
    public async Task Trend_NoData_StillProducesAllZeroPoints()
    {
        // AF-01 — khoảng trống vẫn phải vẽ được, không được ném lỗi hay trả dãy rỗng.
        var result = await _sut.GetStatisticsAsync("2026-07-01", "2026-07-03", TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Trend.Count);
        Assert.All(result.Trend, p =>
        {
            Assert.Equal(0, p.NewAccounts);
            Assert.Equal(0, p.Cases);
            Assert.Equal(0, p.Appointments);
            Assert.Equal(0m, p.Revenue);
        });
    }

    [Fact]
    public async Task NoData_RevenueAndTopMedicines_ReturnAllZerosAndEmptyList()
    {
        var result = await _sut.GetStatisticsAsync(null, null, TestContext.Current.CancellationToken);

        Assert.Equal(0m, result.Revenue.TotalRevenue);
        Assert.Equal(0, result.Revenue.PaidInvoiceCount);
        Assert.Equal(0m, result.Revenue.CashRevenue);
        Assert.Equal(0m, result.Revenue.BankTransferRevenue);
        Assert.Equal(0, result.Revenue.PendingInvoiceCount);
        Assert.Empty(result.TopMedicines);
    }

    [Fact]
    public async Task Revenue_MapsFromRepositoryCorrectly()
    {
        _repo.Setup(r => r.GetRevenueAsync(
                 It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new RevenueCounts(
                 TotalRevenue: 5_000_000m,
                 PaidInvoiceCount: 10,
                 CashRevenue: 3_000_000m, CashCount: 6,
                 BankTransferRevenue: 2_000_000m, BankTransferCount: 4,
                 PendingInvoiceCount: 3, PendingAmount: 800_000m));

        var result = await _sut.GetStatisticsAsync(null, null, TestContext.Current.CancellationToken);

        Assert.Equal(5_000_000m, result.Revenue.TotalRevenue);
        Assert.Equal(10, result.Revenue.PaidInvoiceCount);
        Assert.Equal(3_000_000m, result.Revenue.CashRevenue);
        Assert.Equal(6, result.Revenue.CashCount);
        Assert.Equal(2_000_000m, result.Revenue.BankTransferRevenue);
        Assert.Equal(4, result.Revenue.BankTransferCount);
        Assert.Equal(3, result.Revenue.PendingInvoiceCount);
        Assert.Equal(800_000m, result.Revenue.PendingAmount);
    }

    [Fact]
    public async Task TopMedicines_MapsFromRepositoryCorrectly()
    {
        var medId = Guid.NewGuid();
        _repo.Setup(r => r.GetTopPrescribedMedicinesAsync(
                 It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<TopMedicine>
             {
                 new(medId, "Paracetamol 500mg", 25, 750),
             });

        var result = await _sut.GetStatisticsAsync(null, null, TestContext.Current.CancellationToken);

        Assert.Single(result.TopMedicines);
        Assert.Equal(medId, result.TopMedicines[0].MedicineId);
        Assert.Equal("Paracetamol 500mg", result.TopMedicines[0].MedicineName);
        Assert.Equal(25, result.TopMedicines[0].PrescriptionCount);
        Assert.Equal(750, result.TopMedicines[0].TotalQuantityBase);
    }

    [Fact]
    public async Task Trend_IncludesRevenue_FillsZeroForEmptyDays()
    {
        _repo.Setup(r => r.GetDailyActivityAsync(
                 It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<DailyActivity>
             {
                 new(new DateOnly(2026, 7, 2), 0, 0, 0, 500_000m),
             });

        var result = await _sut.GetStatisticsAsync("2026-07-01", "2026-07-03", TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Trend.Count);
        Assert.Equal(0m, result.Trend[0].Revenue);
        Assert.Equal(500_000m, result.Trend[1].Revenue);
        Assert.Equal(0m, result.Trend[2].Revenue);
    }

    // ---------- helpers ----------

    private static ActivityCounts NoActivity() =>
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private void SetupActivity(
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
                 It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
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
