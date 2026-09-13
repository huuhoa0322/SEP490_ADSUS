using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.DashboardReporting.DTOs;
using ADSUS_BE.BLL.DashboardReporting.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Implementations;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.DashboardReporting;

/// <summary>
/// Comprehensive QA Test Suite verifying Gross Profit, COGS, Service vs Medicine Revenue,
/// and Profit Margin for DashboardReporting feature across all business edge cases.
/// </summary>
public class DashboardRevenueProfitComprehensiveTests
{
    private static AppDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    private static DashboardService CreateService(IDashboardRepository repo)
    {
        var aiModelRepo = new Mock<IAiModelVersionRepository>();
        return new DashboardService(repo, aiModelRepo.Object);
    }

    // =========================================================================
    // Case 1: Hóa đơn chỉ có dịch vụ (không có thuốc)
    // -> Doanh thu thuốc = 0, Giá vốn = 0, Tiền lãi = Doanh thu dịch vụ, Margin = 100%.
    // =========================================================================
    [Fact]
    public async Task Case1_ServiceOnly_MedicineRevenueAndCostZero_ProfitEqualsServiceRevenue_Margin100()
    {
        await using var db = CreateContext();
        var from = new DateOnly(2026, 8, 1);
        var to = new DateOnly(2026, 8, 5);
        var paidAtUtc = ClinicClock.StartOfDayUtc(from).AddHours(3);

        var inv = BuildInvoice(InvoiceStatus.PAID, 500_000m, PaymentMethod.CASH, paidAtUtc);
        db.Invoices.Add(inv);
        db.InvoiceItems.Add(BuildInvoiceItem(inv.Id, InvoiceItemType.Service, 500_000m));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new DashboardRepository(db);
        var sut = CreateService(repo);

        var repoRev = await repo.GetRevenueAsync(from, to, CancellationToken.None);
        Assert.Equal(500_000m, repoRev.TotalRevenue);
        Assert.Equal(500_000m, repoRev.ServiceRevenue);
        Assert.Equal(0m, repoRev.MedicineRevenue);
        Assert.Equal(0m, repoRev.MedicineCost);
        Assert.Equal(0m, repoRev.MedicineProfit);
        Assert.Equal(500_000m, repoRev.TotalProfit);

        var stats = await sut.GetStatisticsAsync("2026-08-01", "2026-08-05", CancellationToken.None);
        Assert.Equal(500_000m, stats.Revenue.TotalProfit);
        Assert.Equal(100.0, stats.Revenue.ProfitMargin);
    }

    // =========================================================================
    // Case 2: Hóa đơn chỉ có thuốc
    // -> Doanh thu dịch vụ = 0, Tiền lãi = Doanh thu thuốc - Giá vốn thuốc.
    // =========================================================================
    [Fact]
    public async Task Case2_MedicineOnly_ServiceRevenueZero_ProfitEqualsMedicineRevenueMinusCost()
    {
        await using var db = CreateContext();
        var from = new DateOnly(2026, 8, 1);
        var to = new DateOnly(2026, 8, 5);
        var paidAtUtc = ClinicClock.StartOfDayUtc(from).AddHours(4);

        var inv = BuildInvoice(InvoiceStatus.PAID, 400_000m, PaymentMethod.BANK_TRANSFER, paidAtUtc);
        db.Invoices.Add(inv);

        var pItemId = Guid.NewGuid();
        db.InvoiceItems.Add(BuildInvoiceItem(inv.Id, InvoiceItemType.Medicine, 400_000m, pItemId));
        // Dispense 10 units @ 25,000 = 250,000 cost
        db.InventoryTransactions.Add(BuildInventoryTxn(pItemId, InventoryTxnType.Dispense, 10, 25_000m));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new DashboardRepository(db);
        var sut = CreateService(repo);

        var repoRev = await repo.GetRevenueAsync(from, to, CancellationToken.None);
        Assert.Equal(400_000m, repoRev.TotalRevenue);
        Assert.Equal(0m, repoRev.ServiceRevenue);
        Assert.Equal(400_000m, repoRev.MedicineRevenue);
        Assert.Equal(250_000m, repoRev.MedicineCost);
        Assert.Equal(150_000m, repoRev.MedicineProfit);
        Assert.Equal(150_000m, repoRev.TotalProfit);

        var stats = await sut.GetStatisticsAsync("2026-08-01", "2026-08-05", CancellationToken.None);
        Assert.Equal(150_000m, stats.Revenue.TotalProfit);
        Assert.Equal(37.5, stats.Revenue.ProfitMargin); // 150_000 / 400_000 * 100 = 37.5%
    }

    // =========================================================================
    // Case 3: Hóa đơn kết hợp cả dịch vụ và thuốc
    // -> Tiền lãi = Doanh thu dịch vụ + (Doanh thu thuốc - Giá vốn thuốc).
    // =========================================================================
    [Fact]
    public async Task Case3_CombinedServiceAndMedicine_ProfitSumsCorrectly()
    {
        await using var db = CreateContext();
        var from = new DateOnly(2026, 8, 1);
        var to = new DateOnly(2026, 8, 5);
        var paidAtUtc = ClinicClock.StartOfDayUtc(from).AddHours(5);

        var inv = BuildInvoice(InvoiceStatus.PAID, 800_000m, PaymentMethod.CASH, paidAtUtc);
        db.Invoices.Add(inv);

        var pItemId = Guid.NewGuid();
        db.InvoiceItems.AddRange(
            BuildInvoiceItem(inv.Id, InvoiceItemType.Service, 300_000m),
            BuildInvoiceItem(inv.Id, InvoiceItemType.Medicine, 500_000m, pItemId));

        // Dispense 10 units @ 20,000 = 200,000 cost
        db.InventoryTransactions.Add(BuildInventoryTxn(pItemId, InventoryTxnType.Dispense, 10, 20_000m));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new DashboardRepository(db);
        var sut = CreateService(repo);

        var repoRev = await repo.GetRevenueAsync(from, to, CancellationToken.None);
        Assert.Equal(800_000m, repoRev.TotalRevenue);
        Assert.Equal(300_000m, repoRev.ServiceRevenue);
        Assert.Equal(500_000m, repoRev.MedicineRevenue);
        Assert.Equal(200_000m, repoRev.MedicineCost);
        Assert.Equal(300_000m, repoRev.MedicineProfit); // 500k - 200k = 300k
        Assert.Equal(600_000m, repoRev.TotalProfit);    // 300k service + 300k med profit = 600k

        var stats = await sut.GetStatisticsAsync("2026-08-01", "2026-08-05", CancellationToken.None);
        Assert.Equal(600_000m, stats.Revenue.TotalProfit);
        Assert.Equal(75.0, stats.Revenue.ProfitMargin); // 600_000 / 800_000 * 100 = 75.0%
    }

    // =========================================================================
    // Case 4: Thuốc xuất từ nhiều lô khác nhau (nhiều dispense transactions với ActualImportPrice khác nhau theo FEFO)
    // -> tổng giá vốn phải cộng dồn chính xác.
    // =========================================================================
    [Fact]
    public async Task Case4_MultipleBatchesFefo_CostAccumulatesAccurately()
    {
        await using var db = CreateContext();
        var from = new DateOnly(2026, 8, 1);
        var to = new DateOnly(2026, 8, 5);
        var paidAtUtc = ClinicClock.StartOfDayUtc(from).AddHours(2);

        var inv = BuildInvoice(InvoiceStatus.PAID, 1_000_000m, PaymentMethod.CASH, paidAtUtc);
        db.Invoices.Add(inv);

        var pItemId = Guid.NewGuid();
        db.InvoiceItems.Add(BuildInvoiceItem(inv.Id, InvoiceItemType.Medicine, 1_000_000m, pItemId));

        // Batch 1: 20 @ 10,000 = 200,000
        // Batch 2: 15 @ 12,000 = 180,000
        // Batch 3: 10 @ 15,000 = 150,000
        // Total expected cost = 530,000
        db.InventoryTransactions.AddRange(
            BuildInventoryTxn(pItemId, InventoryTxnType.Dispense, 20, 10_000m),
            BuildInventoryTxn(pItemId, InventoryTxnType.Dispense, 15, 12_000m),
            BuildInventoryTxn(pItemId, InventoryTxnType.Dispense, 10, 15_000m));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new DashboardRepository(db);
        var sut = CreateService(repo);

        var repoRev = await repo.GetRevenueAsync(from, to, CancellationToken.None);
        Assert.Equal(530_000m, repoRev.MedicineCost);
        Assert.Equal(470_000m, repoRev.MedicineProfit);
        Assert.Equal(470_000m, repoRev.TotalProfit);

        var stats = await sut.GetStatisticsAsync("2026-08-01", "2026-08-05", CancellationToken.None);
        Assert.Equal(47.0, stats.Revenue.ProfitMargin); // 470_000 / 1_000_000 * 100 = 47.0%
    }

    // =========================================================================
    // Case 5: Hóa đơn có hoàn thuốc tự động (Adjustment transaction)
    // -> giá vốn thuốc được hoàn trừ tương ứng.
    // =========================================================================
    [Fact]
    public async Task Case5_RefundAdjustmentTransaction_DeductsFromCostAccurately()
    {
        await using var db = CreateContext();
        var from = new DateOnly(2026, 8, 1);
        var to = new DateOnly(2026, 8, 5);
        var paidAtUtc = ClinicClock.StartOfDayUtc(from).AddHours(6);

        var inv = BuildInvoice(InvoiceStatus.PAID, 600_000m, PaymentMethod.CASH, paidAtUtc);
        db.Invoices.Add(inv);

        var pItemId = Guid.NewGuid();
        db.InvoiceItems.Add(BuildInvoiceItem(inv.Id, InvoiceItemType.Medicine, 600_000m, pItemId));

        // Dispense: 20 units @ 15,000 = 300,000
        // Refund Adjustment: 5 units @ 15,000 = -75,000
        // Net Cost = 225,000
        db.InventoryTransactions.AddRange(
            BuildInventoryTxn(pItemId, InventoryTxnType.Dispense, 20, 15_000m),
            BuildInventoryTxn(pItemId, InventoryTxnType.Adjustment, 5, 15_000m));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new DashboardRepository(db);
        var sut = CreateService(repo);

        var repoRev = await repo.GetRevenueAsync(from, to, CancellationToken.None);
        Assert.Equal(225_000m, repoRev.MedicineCost);
        Assert.Equal(375_000m, repoRev.MedicineProfit);
        Assert.Equal(375_000m, repoRev.TotalProfit);

        var stats = await sut.GetStatisticsAsync("2026-08-01", "2026-08-05", CancellationToken.None);
        Assert.Equal(62.5, stats.Revenue.ProfitMargin); // 375_000 / 600_000 * 100 = 62.5%
    }

    // =========================================================================
    // Case 6: Hóa đơn PENDING hoặc CANCELLED
    // -> tuyệt đối KHÔNG được tính vào doanh thu hoặc tiền lãi.
    // =========================================================================
    [Fact]
    public async Task Case6_PendingOrCancelledInvoices_ExcludedFromRevenueAndProfit()
    {
        await using var db = CreateContext();
        var from = new DateOnly(2026, 8, 1);
        var to = new DateOnly(2026, 8, 5);
        var paidAtUtc = ClinicClock.StartOfDayUtc(from).AddHours(2);

        // 1 Paid Invoice (Service: 100_000)
        var paidInv = BuildInvoice(InvoiceStatus.PAID, 100_000m, PaymentMethod.CASH, paidAtUtc);
        db.Invoices.Add(paidInv);
        db.InvoiceItems.Add(BuildInvoiceItem(paidInv.Id, InvoiceItemType.Service, 100_000m));

        // 1 Pending Invoice (700_000)
        var pendingInv = BuildInvoice(InvoiceStatus.PENDING, 700_000m, null, null);
        db.Invoices.Add(pendingInv);
        var pItemPending = Guid.NewGuid();
        db.InvoiceItems.AddRange(
            BuildInvoiceItem(pendingInv.Id, InvoiceItemType.Service, 200_000m),
            BuildInvoiceItem(pendingInv.Id, InvoiceItemType.Medicine, 500_000m, pItemPending));
        db.InventoryTransactions.Add(BuildInventoryTxn(pItemPending, InventoryTxnType.Dispense, 10, 30_000m));

        // 1 Cancelled Invoice (1_000_000)
        var cancelledInv = BuildInvoice(InvoiceStatus.CANCELLED, 1_000_000m, null, null);
        db.Invoices.Add(cancelledInv);
        var pItemCancelled = Guid.NewGuid();
        db.InvoiceItems.AddRange(
            BuildInvoiceItem(cancelledInv.Id, InvoiceItemType.Service, 300_000m),
            BuildInvoiceItem(cancelledInv.Id, InvoiceItemType.Medicine, 700_000m, pItemCancelled));
        db.InventoryTransactions.Add(BuildInventoryTxn(pItemCancelled, InventoryTxnType.Dispense, 20, 20_000m));

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new DashboardRepository(db);
        var sut = CreateService(repo);

        var repoRev = await repo.GetRevenueAsync(from, to, CancellationToken.None);
        Assert.Equal(100_000m, repoRev.TotalRevenue);
        Assert.Equal(100_000m, repoRev.ServiceRevenue);
        Assert.Equal(0m, repoRev.MedicineRevenue);
        Assert.Equal(0m, repoRev.MedicineCost);
        Assert.Equal(100_000m, repoRev.TotalProfit);
        Assert.Equal(1, repoRev.PendingInvoiceCount);
        Assert.Equal(700_000m, repoRev.PendingAmount);

        var stats = await sut.GetStatisticsAsync("2026-08-01", "2026-08-05", CancellationToken.None);
        Assert.Equal(100_000m, stats.Revenue.TotalProfit);
        Assert.Equal(100.0, stats.Revenue.ProfitMargin);
    }

    // =========================================================================
    // Case 7: Giá vốn thực tế (COGS) chỉ dùng ActualImportPrice trong InventoryTransaction,
    // hoàn toàn ĐỘC LẬP và KHÔNG bị ảnh hưởng bởi BaseUnitAvgImportPrice trong MedicineBatch hay bảng Medicine.
    // =========================================================================
    [Fact]
    public async Task Case7_CogsUsesOnlyActualImportPrice_CompletelyIndependentFromMedicineBatchAvgPrice()
    {
        await using var db = CreateContext();
        var from = new DateOnly(2026, 8, 1);
        var to = new DateOnly(2026, 8, 5);
        var paidAtUtc = ClinicClock.StartOfDayUtc(from).AddHours(2);

        // 1. Tạo Medicine và MedicineBatch có BaseUnitAvgImportPrice = 90.000đ
        var medicineId = Guid.NewGuid();
        var med = new Medicine
        {
            MedicineId = medicineId,
            Name = "Kháng sinh Test",
            CreatedAt = DateTime.UtcNow,
            LowStockThreshold = 10
        };
        db.Medicines.Add(med);

        var batch = new MedicineBatch
        {
            Id = Guid.NewGuid(),
            MedicineId = medicineId,
            LotNumber = "LOT-999",
            ExpiryDate = new DateOnly(2027, 1, 1),
            QuantityBase = 100,
            BaseUnitAvgImportPrice = 90_000m // Giá nhập bình quân trong bảng lô thuốc là 90k
        };
        db.MedicineBatches.Add(batch);

        // 2. Tạo Hóa đơn bán thuốc 200.000đ
        var inv = BuildInvoice(InvoiceStatus.PAID, 200_000m, PaymentMethod.CASH, paidAtUtc);
        db.Invoices.Add(inv);

        var pItemId = Guid.NewGuid();
        db.InvoiceItems.Add(BuildInvoiceItem(inv.Id, InvoiceItemType.Medicine, 200_000m, pItemId));

        // 3. Giao dịch xuất kho ghi nhận ActualImportPrice lúc bán = 15.000đ (đã được đóng băng khi xuất)
        // Xuất 10 viên -> Chi phí vốn thực tế lúc xuất phải là: 10 * 15.000đ = 150.000đ
        var txn = BuildInventoryTxn(pItemId, InventoryTxnType.Dispense, 10, 15_000m);
        txn.BatchId = batch.Id;
        db.InventoryTransactions.Add(txn);

        // Giả sử sau đó giá bình quân trong bảng batch bị cập nhật thành 120.000đ (hoặc khác hoàn toàn)
        batch.BaseUnitAvgImportPrice = 120_000m;

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new DashboardRepository(db);
        var sut = CreateService(repo);

        var repoRev = await repo.GetRevenueAsync(from, to, CancellationToken.None);

        // KHẲNG ĐỊNH: Giá vốn được tính CHÍNH XÁC từ InventoryTransaction.ActualImportPrice (10 * 15.000đ = 150.000đ)
        // TUYỆT ĐỐI KHÔNG dùng BaseUnitAvgImportPrice trong MedicineBatch (nếu dùng sẽ ra 900.000đ hoặc 1.200.000đ)
        Assert.Equal(200_000m, repoRev.TotalRevenue);
        Assert.Equal(200_000m, repoRev.MedicineRevenue);
        Assert.Equal(150_000m, repoRev.MedicineCost); // 10 * 15.000đ
        Assert.NotEqual(900_000m, repoRev.MedicineCost); // Không dùng giá lô cũ 90k
        Assert.NotEqual(1_200_000m, repoRev.MedicineCost); // Không dùng giá lô mới 120k

        Assert.Equal(50_000m, repoRev.MedicineProfit); // 200k - 150k = 50k
        Assert.Equal(50_000m, repoRev.TotalProfit);

        var stats = await sut.GetStatisticsAsync("2026-08-01", "2026-08-05", CancellationToken.None);
        Assert.Equal(50_000m, stats.Revenue.TotalProfit);
        Assert.Equal(25.0, stats.Revenue.ProfitMargin); // 50k / 200k * 100 = 25.0%
    }

    // =========================================================================
    // Case 8: Kỳ không có hóa đơn nào (kỳ trống)
    // -> Doanh thu = 0, Giá vốn = 0, Tiền lãi = 0, Margin = 0%, không lỗi chia 0.
    // =========================================================================
    [Fact]
    public async Task Case8_EmptyPeriod_AllZeroes_NoDivideByZeroException()
    {
        await using var db = CreateContext();
        var from = new DateOnly(2026, 8, 1);
        var to = new DateOnly(2026, 8, 5);

        var repo = new DashboardRepository(db);
        var sut = CreateService(repo);

        var repoRev = await repo.GetRevenueAsync(from, to, CancellationToken.None);
        Assert.Equal(0m, repoRev.TotalRevenue);
        Assert.Equal(0m, repoRev.ServiceRevenue);
        Assert.Equal(0m, repoRev.MedicineRevenue);
        Assert.Equal(0m, repoRev.MedicineCost);
        Assert.Equal(0m, repoRev.MedicineProfit);
        Assert.Equal(0m, repoRev.TotalProfit);

        var stats = await sut.GetStatisticsAsync("2026-08-01", "2026-08-05", CancellationToken.None);
        Assert.Equal(0m, stats.Revenue.TotalProfit);
        Assert.Equal(0.0, stats.Revenue.ProfitMargin);
        Assert.NotNull(stats.Trend);
        Assert.All(stats.Trend, p =>
        {
            Assert.Equal(0m, p.Revenue);
            Assert.Equal(0m, p.Profit);
        });
    }

    // =========================================================================
    // Case 9: Trường hợp bán dưới giá vốn (lỗ)
    // -> Tiền lãi âm, Margin âm được tính toán chính xác không bị clamp sai.
    // =========================================================================
    [Fact]
    public async Task Case9_SoldBelowCost_LossScenario_NegativeProfitAndMarginCalculatedCorrectly()
    {
        await using var db = CreateContext();
        var from = new DateOnly(2026, 8, 1);
        var to = new DateOnly(2026, 8, 5);
        var paidAtUtc = ClinicClock.StartOfDayUtc(from).AddHours(2);

        var inv = BuildInvoice(InvoiceStatus.PAID, 100_000m, PaymentMethod.CASH, paidAtUtc);
        db.Invoices.Add(inv);

        var pItemId = Guid.NewGuid();
        db.InvoiceItems.Add(BuildInvoiceItem(inv.Id, InvoiceItemType.Medicine, 100_000m, pItemId));

        // Dispense cost = 10 * 15,000 = 150,000
        db.InventoryTransactions.Add(BuildInventoryTxn(pItemId, InventoryTxnType.Dispense, 10, 15_000m));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new DashboardRepository(db);
        var sut = CreateService(repo);

        var repoRev = await repo.GetRevenueAsync(from, to, CancellationToken.None);
        Assert.Equal(100_000m, repoRev.TotalRevenue);
        Assert.Equal(100_000m, repoRev.MedicineRevenue);
        Assert.Equal(150_000m, repoRev.MedicineCost);
        Assert.Equal(-50_000m, repoRev.MedicineProfit);
        Assert.Equal(-50_000m, repoRev.TotalProfit);

        var stats = await sut.GetStatisticsAsync("2026-08-01", "2026-08-05", CancellationToken.None);
        Assert.Equal(-50_000m, stats.Revenue.TotalProfit);
        Assert.Equal(-50.0, stats.Revenue.ProfitMargin); // -50_000 / 100_000 * 100 = -50.0%
    }

    // =========================================================================
    // Case 10: Phân bổ Tiền lãi theo ngày (DailyActivity / DailyPoint.Profit)
    // -> khớp chính xác với ngày thanh toán của từng hóa đơn.
    // =========================================================================
    [Fact]
    public async Task Case10_DailyProfitAllocation_MatchesPaymentDateOfEachInvoice()
    {
        await using var db = CreateContext();
        var day1 = new DateOnly(2026, 8, 1);
        var day2 = new DateOnly(2026, 8, 2);
        var day3 = new DateOnly(2026, 8, 3);

        // Invoice on Day 1: Service 300,000
        var day1Utc = ClinicClock.StartOfDayUtc(day1).AddHours(3); // 10:00 VN
        var inv1 = BuildInvoice(InvoiceStatus.PAID, 300_000m, PaymentMethod.CASH, day1Utc);
        db.Invoices.Add(inv1);
        db.InvoiceItems.Add(BuildInvoiceItem(inv1.Id, InvoiceItemType.Service, 300_000m));

        // Invoice on Day 2: Medicine 500,000 with cost 200,000 -> Profit 300,000
        var day2Utc = ClinicClock.StartOfDayUtc(day2).AddHours(7); // 14:00 VN
        var inv2 = BuildInvoice(InvoiceStatus.PAID, 500_000m, PaymentMethod.BANK_TRANSFER, day2Utc);
        db.Invoices.Add(inv2);
        var pItemId = Guid.NewGuid();
        db.InvoiceItems.Add(BuildInvoiceItem(inv2.Id, InvoiceItemType.Medicine, 500_000m, pItemId));
        db.InventoryTransactions.Add(BuildInventoryTxn(pItemId, InventoryTxnType.Dispense, 10, 20_000m));

        // Day 3: No invoices
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new DashboardRepository(db);
        var sut = CreateService(repo);

        var daily = await repo.GetDailyActivityAsync(day1, day3, CancellationToken.None);
        Assert.Equal(2, daily.Count);

        var d1 = daily.Single(d => d.Date == day1);
        Assert.Equal(300_000m, d1.Revenue);
        Assert.Equal(300_000m, d1.Profit);

        var d2 = daily.Single(d => d.Date == day2);
        Assert.Equal(500_000m, d2.Revenue);
        Assert.Equal(300_000m, d2.Profit); // 500k - 200k = 300k

        var stats = await sut.GetStatisticsAsync("2026-08-01", "2026-08-03", CancellationToken.None);
        Assert.Equal(3, stats.Trend.Count);

        var t1 = stats.Trend[0];
        Assert.Equal("2026-08-01", t1.Date);
        Assert.Equal(300_000m, t1.Revenue);
        Assert.Equal(300_000m, t1.Profit);

        var t2 = stats.Trend[1];
        Assert.Equal("2026-08-02", t2.Date);
        Assert.Equal(500_000m, t2.Revenue);
        Assert.Equal(300_000m, t2.Profit);

        var t3 = stats.Trend[2];
        Assert.Equal("2026-08-03", t3.Date);
        Assert.Equal(0m, t3.Revenue);
        Assert.Equal(0m, t3.Profit);
    }

    // =========================================================================
    // Case 11: Một hóa đơn có nhiều dòng thuốc cùng tham chiếu 1 PrescriptionItemId
    // -> Giá vốn không bị nhân đôi trong DailyActivity.
    // =========================================================================
    [Fact]
    public async Task Case11_MultipleInvoiceItemsWithSamePrescriptionItemId_DailyActivityDoesNotDoubleCountCogs()
    {
        await using var db = CreateContext();
        var day = new DateOnly(2026, 8, 1);
        var paidAtUtc = ClinicClock.StartOfDayUtc(day).AddHours(4);

        var inv = BuildInvoice(InvoiceStatus.PAID, 100_000m, PaymentMethod.CASH, paidAtUtc);
        db.Invoices.Add(inv);

        var pItemId = Guid.NewGuid();
        // 2 items in same invoice referencing same pItemId
        db.InvoiceItems.AddRange(
            BuildInvoiceItem(inv.Id, InvoiceItemType.Medicine, 49_000m, pItemId),
            BuildInvoiceItem(inv.Id, InvoiceItemType.Medicine, 51_000m, pItemId));

        // Cost for this prescription item = 48_000
        db.InventoryTransactions.Add(BuildInventoryTxn(pItemId, InventoryTxnType.Dispense, 2, 24_000m));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new DashboardRepository(db);
        var sut = CreateService(repo);

        var rev = await repo.GetRevenueAsync(day, day, CancellationToken.None);
        Assert.Equal(48_000m, rev.MedicineCost);
        Assert.Equal(52_000m, rev.TotalProfit);

        var daily = await repo.GetDailyActivityAsync(day, day, CancellationToken.None);
        var point = Assert.Single(daily);
        Assert.Equal(100_000m, point.Revenue);
        Assert.Equal(52_000m, point.Profit); // Must be 100k - 48k = 52k, NOT 100k - 96k = 4k!

        var stats = await sut.GetStatisticsAsync("2026-08-01", "2026-08-01", CancellationToken.None);
        Assert.Equal(52_000m, stats.Trend[0].Profit);
        Assert.Equal(stats.Revenue.TotalProfit, stats.Trend.Sum(t => t.Profit));
    }

    // ---------- Helper Methods ----------

    private static Invoice BuildInvoice(InvoiceStatus status, decimal total, PaymentMethod? method, DateTime? paidAt) => new()
    {
        Id = Guid.NewGuid(),
        CaseId = Guid.NewGuid(),
        TotalAmount = total,
        Status = status,
        PaymentMethod = method,
        PaidAt = paidAt,
        CreatedAt = DateTime.UtcNow,
    };

    private static InvoiceItem BuildInvoiceItem(Guid invoiceId, InvoiceItemType type, decimal totalPrice, Guid? referenceId = null) => new()
    {
        Id = Guid.NewGuid(),
        InvoiceId = invoiceId,
        Description = "Test item",
        Quantity = 1,
        UnitPrice = totalPrice,
        TotalPrice = totalPrice,
        ItemType = type,
        ReferenceId = referenceId,
    };

    private static InventoryTransaction BuildInventoryTxn(Guid prescriptionItemId, InventoryTxnType txnType, int qtyBase, decimal actualImportPrice) => new()
    {
        Id = Guid.NewGuid(),
        BatchId = Guid.NewGuid(),
        MedicinePackagingId = Guid.NewGuid(),
        TxnDate = DateTime.UtcNow,
        PrescriptionItemId = prescriptionItemId,
        TxnType = txnType,
        QuantityBase = qtyBase,
        QuantityInUnit = qtyBase,
        ActualImportPrice = actualImportPrice,
    };
}
