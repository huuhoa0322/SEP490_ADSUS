using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>
/// UC-05 FT-10 — đếm số liệu cho màn thống kê.
///
/// Mọi truy vấn đều là đếm thuần. Không truy vấn nào lấy về tên, số điện thoại hay bản ghi
/// bệnh nhân (BR-01) — dữ liệu cá nhân không rời khỏi database, nên không có đường nào lọt
/// lên màn hình Admin dù có lỗi lập trình ở tầng trên.
/// </summary>
public class DashboardRepository : IDashboardRepository
{
    private readonly AppDbContext _db;

    public DashboardRepository(AppDbContext db) => _db = db;

    public async Task<AccountCounts> GetAccountCountsAsync(
        CancellationToken cancellationToken = default)
    {
        // Gom về MỘT truy vấn nhóm theo (vai trò, trạng thái) rồi cộng ở bộ nhớ, thay vì
        // bắn 8 lệnh COUNT riêng. Bảng users nhỏ nên chênh lệch không lớn, nhưng 8 vòng đi
        // về tới Supabase ở Singapore thì độ trễ mạng cộng dồn thấy rõ.
        var groups = await _db.Users
            .AsNoTracking()
            .GroupBy(u => new { u.Role, u.Status })
            .Select(g => new { g.Key.Role, g.Key.Status, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int ByRole(UserRole role) => groups.Where(g => g.Role == role).Sum(g => g.Count);
        int ByStatus(UserStatus status) => groups.Where(g => g.Status == status).Sum(g => g.Count);

        return new AccountCounts(
            Total: groups.Sum(g => g.Count),
            AdminCount: ByRole(UserRole.Admin),
            DoctorCount: ByRole(UserRole.Doctor),
            NurseCount: ByRole(UserRole.Staff),
            PatientCount: ByRole(UserRole.Patient),
            ActiveCount: ByStatus(UserStatus.Active),
            DeactivatedCount: ByStatus(UserStatus.Deactivated));
    }

    public async Task<ActivityCounts> GetActivityCountsAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        // Cột lưu mốc thời gian là UTC, còn khoảng ngày nhận vào là ngày ở phòng khám —
        // quy đổi qua ClinicClock. Cột lưu ngày thuần thì so trực tiếp, không đụng múi giờ.
        var fromInclusive = ClinicClock.StartOfDayUtc(fromDate);
        var toExclusive = ClinicClock.EndOfDayExclusiveUtc(toDate);

        // Lọc theo CreatedAt của từng bảng. Cases dùng VisitDate vì đó mới là ngày khám thật;
        // CreatedAt chỉ là lúc bấm lưu, có thể lệch ngày nếu bác sĩ nhập bù hôm sau.
        var newAccounts = await _db.Users.AsNoTracking()
            .CountAsync(u => u.CreatedAt >= fromInclusive && u.CreatedAt < toExclusive, cancellationToken);

        var caseCount = await _db.Cases.AsNoTracking()
            .CountAsync(c => c.VisitDate >= fromDate && c.VisitDate <= toDate, cancellationToken);

        var aiImageIds = await _db.AiPredictions.AsNoTracking()
            .Where(p => p.CreatedAt >= fromInclusive && p.CreatedAt < toExclusive)
            .Select(p => p.ImageId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var aiRunCount = aiImageIds.Count;

        var reviewedImageIds = await _db.DoctorAnnotations.AsNoTracking()
            .Where(da => aiImageIds.Contains(da.ImageId))
            .Select(da => da.ImageId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var aiConfirmed = reviewedImageIds.Count;
        var aiPending = aiRunCount - aiConfirmed;
        var aiRejected = 0;

        // Lọc lịch hẹn theo NGÀY KHÁM (SlotDate), không theo ngày đặt.
        //
        // Phải cùng mốc với khung giờ bên dưới, nếu không "số lượt đặt trung bình mỗi khung
        // giờ" là đem hai đại lượng khác mốc chia cho nhau. Thực tế gặp phải: khung giờ nằm
        // ở tương lai nên rơi hết ra ngoài khoảng 30 ngày vừa qua, ra 10 lượt đặt trên 0
        // khung giờ.
        //
        // Đọc theo ngày khám cũng đúng nghĩa hơn với Admin: "kỳ này phòng khám có bao nhiêu
        // lượt hẹn" chứ không phải "bao nhiêu lượt được bấm đặt".
        var appointmentGroups = await _db.Appointments.AsNoTracking()
            .Where(a => a.Slot.SlotDate >= fromDate && a.Slot.SlotDate <= toDate)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int Appointments(AppointmentStatus status) =>
            appointmentGroups.FirstOrDefault(g => g.Status == status)?.Count ?? 0;

        // FR §3 đòi "currently Open schedule slots", không phải mọi slot rơi vào khoảng ngày
        // đang chọn bất kể trạng thái — một slot đã Booked/Closed không còn "mở" nữa.
        var slotCount = await _db.ScheduleSlots.AsNoTracking()
            .CountAsync(
                s => s.SlotDate >= fromDate && s.SlotDate <= toDate && s.Status == SlotStatus.Open,
                cancellationToken);

        // Tuân thủ uống thuốc: đếm theo giờ ĐƯỢC HẸN, không phải giờ xác nhận. Liều hẹn hôm
        // qua mà sáng nay mới bấm xác nhận vẫn phải tính vào hôm qua, nếu không tỉ lệ tuân
        // thủ của mọi ngày đều sai.
        //
        // Trạng thái chỉ có Pending → Taken (Glossary UCS), nên "đã uống" tương đương
        // ConfirmedAt khác null — không cần tới enum intake_status.
        var doseCount = await _db.MedicationIntakeLogs.AsNoTracking()
            .CountAsync(l => l.ScheduledTime >= fromInclusive && l.ScheduledTime < toExclusive,
                cancellationToken);

        var takenCount = await _db.MedicationIntakeLogs.AsNoTracking()
            .CountAsync(l => l.ScheduledTime >= fromInclusive
                             && l.ScheduledTime < toExclusive
                             && l.ConfirmedAt != null,
                cancellationToken);

        return BuildActivityCounts(
            newAccounts,
            caseCount,
            aiRunCount,
            aiConfirmed,
            aiRejected,
            aiPending,
            Appointments(AppointmentStatus.Booked),
            Appointments(AppointmentStatus.Cancelled),
            slotCount,
            doseCount,
            takenCount);
    }

    public async Task<IReadOnlyList<DailyActivity>> GetDailyActivityAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        var fromInclusive = ClinicClock.StartOfDayUtc(fromDate);
        var toExclusive = ClinicClock.EndOfDayExclusiveUtc(toDate);

        // Ba truy vấn nhóm riêng rồi ghép ở bộ nhớ. Không JOIN được vì ba bảng này không có
        // quan hệ nào với nhau — ghép bằng ngày là cách duy nhất đúng.
        //
        // Cộng độ lệch múi giờ TRƯỚC khi cắt lấy ngày, nếu không thì tài khoản tạo trong
        // khoảng 00:00–07:00 giờ Việt Nam bị xếp nhầm sang cột hôm trước trên biểu đồ.
        var accountsByDay = await _db.Users.AsNoTracking()
            .Where(u => u.CreatedAt >= fromInclusive && u.CreatedAt < toExclusive)
            .GroupBy(u => DateOnly.FromDateTime(u.CreatedAt.AddHours(ClinicClock.OffsetHours)))
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var casesByDay = await _db.Cases.AsNoTracking()
            .Where(c => c.VisitDate >= fromDate && c.VisitDate <= toDate)
            .GroupBy(c => c.VisitDate)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // Cùng mốc ngày khám như GetActivityCountsAsync, để hai chỗ không nói hai con số khác nhau.
        var appointmentsByDay = await _db.Appointments.AsNoTracking()
            .Where(a => a.Slot.SlotDate >= fromDate && a.Slot.SlotDate <= toDate)
            .GroupBy(a => a.Slot.SlotDate)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var paidInvoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.PAID && i.PaidAt >= fromInclusive && i.PaidAt < toExclusive)
            .Select(i => new
            {
                i.Id,
                i.TotalAmount,
                Date = DateOnly.FromDateTime(i.PaidAt!.Value.AddHours(ClinicClock.OffsetHours))
            })
            .ToListAsync(cancellationToken);

        var medicineItems = await (
            from item in _db.InvoiceItems.AsNoTracking()
            join inv in _db.Invoices.AsNoTracking() on item.InvoiceId equals inv.Id
            where inv.Status == InvoiceStatus.PAID
               && inv.PaidAt >= fromInclusive
               && inv.PaidAt < toExclusive
               && item.ItemType == InvoiceItemType.Medicine
               && item.ReferenceId != null
            select new
            {
                item.InvoiceId,
                PrescriptionItemId = item.ReferenceId!.Value
            }
        ).ToListAsync(cancellationToken);

        var pItemIds = medicineItems.Select(m => m.PrescriptionItemId).Distinct().ToList();
        var costByPrescriptionItem = new Dictionary<Guid, decimal>();

        if (pItemIds.Count > 0)
        {
            var txns = new List<(Guid PrescriptionItemId, InventoryTxnType TxnType, int QuantityBase, decimal ActualImportPrice)>();
            foreach (var chunk in pItemIds.Chunk(1000))
            {
                var chunkTxns = await _db.InventoryTransactions.AsNoTracking()
                    .Where(t => t.PrescriptionItemId != null && chunk.Contains(t.PrescriptionItemId.Value))
                    .Select(t => new
                    {
                        PrescriptionItemId = t.PrescriptionItemId!.Value,
                        t.TxnType,
                        t.QuantityBase,
                        ActualImportPrice = t.ActualImportPrice ?? 0m
                    })
                    .ToListAsync(cancellationToken);

                foreach (var t in chunkTxns)
                {
                    txns.Add((t.PrescriptionItemId, t.TxnType, t.QuantityBase, t.ActualImportPrice));
                }
            }

            costByPrescriptionItem = txns
                .GroupBy(t => t.PrescriptionItemId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(t => t.TxnType == InventoryTxnType.Dispense
                        ? (t.QuantityBase * t.ActualImportPrice)
                        : (t.TxnType == InventoryTxnType.Adjustment ? -(t.QuantityBase * t.ActualImportPrice) : 0m))
                );
        }

        var invoiceToPItems = medicineItems
            .GroupBy(m => m.InvoiceId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.PrescriptionItemId).Distinct().ToList());

        static decimal SumItemCosts(List<Guid> itemIds, Dictionary<Guid, decimal> costMap) =>
            itemIds.Sum(id => costMap.GetValueOrDefault(id, 0m));

        var dailyFinances = paidInvoices
            .GroupBy(i => i.Date)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var rev = g.Sum(i => i.TotalAmount);
                    var cost = g.Sum(i =>
                        invoiceToPItems.TryGetValue(i.Id, out var itemIds)
                            ? SumItemCosts(itemIds, costByPrescriptionItem)
                            : 0m);
                    return new { Revenue = rev, Profit = rev - cost };
                });

        var datesWithData = accountsByDay.Select(x => x.Date)
            .Union(casesByDay.Select(x => x.Date))
            .Union(appointmentsByDay.Select(x => x.Date))
            .Union(dailyFinances.Keys)
            .OrderBy(d => d);

        return datesWithData
            .Select(date =>
            {
                dailyFinances.TryGetValue(date, out var fin);
                return new DailyActivity(
                    date,
                    accountsByDay.FirstOrDefault(x => x.Date == date)?.Count ?? 0,
                    casesByDay.FirstOrDefault(x => x.Date == date)?.Count ?? 0,
                    appointmentsByDay.FirstOrDefault(x => x.Date == date)?.Count ?? 0,
                    fin?.Revenue ?? 0m,
                    fin?.Profit ?? 0m);
            })
            .ToList();
    }

    public async Task<RevenueCounts> GetRevenueAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        var fromInclusive = ClinicClock.StartOfDayUtc(fromDate);
        var toExclusive = ClinicClock.EndOfDayExclusiveUtc(toDate);

        var paidInvoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.PAID && i.PaidAt >= fromInclusive && i.PaidAt < toExclusive)
            .Select(i => new { i.TotalAmount, i.PaymentMethod })
            .ToListAsync(cancellationToken);

        var pendingInvoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.PENDING)
            .Select(i => i.TotalAmount)
            .ToListAsync(cancellationToken);

        var cashInvoices = paidInvoices.Where(i => i.PaymentMethod == PaymentMethod.CASH).ToList();
        var bankInvoices = paidInvoices.Where(i => i.PaymentMethod == PaymentMethod.BANK_TRANSFER).ToList();

        // 1. Phân loại doanh thu từ InvoiceItems của các hóa đơn đã thanh toán trong kỳ
        var paidInvoiceItems = await (
            from item in _db.InvoiceItems.AsNoTracking()
            join inv in _db.Invoices.AsNoTracking() on item.InvoiceId equals inv.Id
            where inv.Status == InvoiceStatus.PAID
               && inv.PaidAt >= fromInclusive
               && inv.PaidAt < toExclusive
            select new
            {
                item.ItemType,
                item.TotalPrice,
                item.ReferenceId
            }
        ).ToListAsync(cancellationToken);

        var serviceRevenue = paidInvoiceItems
            .Where(i => i.ItemType == InvoiceItemType.Service)
            .Sum(i => i.TotalPrice);

        var medicineRevenue = paidInvoiceItems
            .Where(i => i.ItemType == InvoiceItemType.Medicine)
            .Sum(i => i.TotalPrice);

        // 2. Tính giá vốn thuốc (COGS) từ InventoryTransactions
        var prescriptionItemIds = paidInvoiceItems
            .Where(i => i.ItemType == InvoiceItemType.Medicine && i.ReferenceId != null)
            .Select(i => i.ReferenceId!.Value)
            .Distinct()
            .ToList();

        decimal medicineCost = 0m;
        if (prescriptionItemIds.Count > 0)
        {
            foreach (var chunk in prescriptionItemIds.Chunk(1000))
            {
                var chunkTxns = await _db.InventoryTransactions.AsNoTracking()
                    .Where(t => t.PrescriptionItemId != null && chunk.Contains(t.PrescriptionItemId.Value))
                    .Select(t => new
                    {
                        t.TxnType,
                        t.QuantityBase,
                        ActualImportPrice = t.ActualImportPrice ?? 0m
                    })
                    .ToListAsync(cancellationToken);

                medicineCost += chunkTxns.Sum(t => t.TxnType == InventoryTxnType.Dispense
                    ? (t.QuantityBase * t.ActualImportPrice)
                    : (t.TxnType == InventoryTxnType.Adjustment ? -(t.QuantityBase * t.ActualImportPrice) : 0m));
            }
        }

        var totalRevenue = paidInvoices.Sum(i => i.TotalAmount);
        var medicineProfit = medicineRevenue - medicineCost;
        var totalProfit = totalRevenue - medicineCost;

        return new RevenueCounts(
            TotalRevenue: totalRevenue,
            PaidInvoiceCount: paidInvoices.Count,
            CashRevenue: cashInvoices.Sum(i => i.TotalAmount),
            CashCount: cashInvoices.Count,
            BankTransferRevenue: bankInvoices.Sum(i => i.TotalAmount),
            BankTransferCount: bankInvoices.Count,
            PendingInvoiceCount: pendingInvoices.Count,
            PendingAmount: pendingInvoices.Sum(),
            ServiceRevenue: serviceRevenue,
            MedicineRevenue: medicineRevenue,
            MedicineCost: medicineCost,
            MedicineProfit: medicineProfit,
            TotalProfit: totalProfit);
    }

    public async Task<IReadOnlyList<TopMedicine>> GetTopPrescribedMedicinesAsync(
        DateOnly fromDate,
        DateOnly toDate,
        int topN = 10,
        CancellationToken cancellationToken = default)
    {
        if (topN <= 0)
        {
            return Array.Empty<TopMedicine>();
        }

        var fromInclusive = ClinicClock.StartOfDayUtc(fromDate);
        var toExclusive = ClinicClock.EndOfDayExclusiveUtc(toDate);

        var paidMedItems = await (
            from ii in _db.InvoiceItems.AsNoTracking()
            join inv in _db.Invoices.AsNoTracking() on ii.InvoiceId equals inv.Id
            join pi in _db.PrescriptionItems.AsNoTracking() on ii.ReferenceId equals pi.PrescriptionItemId
            join m in _db.Medicines.AsNoTracking().Include(x => x.MedicinePackagings).ThenInclude(p => p.MedicineUnit) on pi.MedicineId equals m.MedicineId
            where inv.Status == InvoiceStatus.PAID
               && inv.PaidAt >= fromInclusive
               && inv.PaidAt < toExclusive
               && ii.ItemType == InvoiceItemType.Medicine
            select new
            {
                ii.Description,
                ii.Quantity,
                ii.UnitPrice,
                Medicine = m,
                PrescriptionId = pi.PrescriptionId
            }
        ).ToListAsync(cancellationToken);

        var grouped = paidMedItems
            .GroupBy(x => new { x.Medicine.MedicineId, x.Medicine.Name })
            .Select(g =>
            {
                var baseUnit = g.First().Medicine.MedicinePackagings.FirstOrDefault(p => p.IsBaseUnit)?.MedicineUnit?.Name
                               ?? g.First().Medicine.UsageUnit
                               ?? "Đơn vị";
                var totalBaseQty = g.Sum(item =>
                {
                    var pack = item.Medicine.MedicinePackagings.FirstOrDefault(p =>
                        item.Description.EndsWith(" - " + p.MedicineUnit?.Name) || item.UnitPrice == p.SalePrice);
                    var factor = pack?.ConversionFactor ?? 1;
                    return item.Quantity * factor;
                });
                var prescriptionCount = g.Select(x => x.PrescriptionId).Distinct().Count();
                return new TopMedicine(
                    g.Key.MedicineId,
                    g.Key.Name,
                    prescriptionCount,
                    totalBaseQty,
                    baseUnit);
            })
            .OrderByDescending(x => x.PrescriptionCount)
            .ThenByDescending(x => x.TotalQuantityBase)
            .Take(topN)
            .ToList();

        return grouped;
    }

    private static ActivityCounts BuildActivityCounts(
        int newAccounts,
        int caseCount,
        int aiRunCount,
        int aiConfirmed,
        int aiRejected,
        int aiPending,
        int booked,
        int cancelled,
        int slotCount,
        int doseCount,
        int takenCount)
    {
        return new ActivityCounts(
            NewAccounts: newAccounts,
            CaseCount: caseCount,
            AiRunCount: aiRunCount,
            AiConfirmedCount: aiConfirmed,
            AiRejectedCount: aiRejected,
            AiPendingCount: aiPending,
            AppointmentBookedCount: booked,
            AppointmentCancelledCount: cancelled,
            ScheduleSlotCount: slotCount,
            MedicationDoseCount: doseCount,
            MedicationTakenCount: takenCount);
    }
}
