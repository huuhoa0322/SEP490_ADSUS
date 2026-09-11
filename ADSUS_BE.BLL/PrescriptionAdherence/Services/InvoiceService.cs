using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs.Invoice;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.DAL.Repositories.Interfaces;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Services;

public class InvoiceService : IInvoiceService
{
    private readonly AppDbContext _context;
    private readonly IInventoryService _inventoryService;
    private readonly IMedicationIntakeLogRepository _intakeLogRepo;
    private readonly IMedicationIntakeScheduleGenerator _scheduleGenerator;
    private readonly INotificationService _notificationService;

    public InvoiceService(
        AppDbContext context,
        IInventoryService inventoryService,
        IMedicationIntakeLogRepository intakeLogRepo,
        IMedicationIntakeScheduleGenerator scheduleGenerator,
        INotificationService notificationService)
    {
        _context = context;
        _inventoryService = inventoryService;
        _intakeLogRepo = intakeLogRepo;
        _scheduleGenerator = scheduleGenerator;
        _notificationService = notificationService;
    }

    public async Task<Guid> GenerateInvoiceForCaseAsync(Guid caseId)
    {
        // 1. Kiểm tra xem Case đã có hóa đơn nào PENDING/PAID chưa để tránh tạo trùng
        var existingInvoice = await _context.Invoices
            .FirstOrDefaultAsync(i => i.CaseId == caseId && (i.Status == InvoiceStatus.PENDING || i.Status == InvoiceStatus.PAID));
            

        
        if (existingInvoice != null)
        {
            return existingInvoice.Id;
        }

        // 2. Lấy đơn thuốc của Case này (chỉ lấy đơn đang Active)
        var prescription = await _context.Prescriptions
            .AsNoTracking()
            .Include(p => p.PrescriptionItems)
                .ThenInclude(pi => pi.Medicine)
            .FirstOrDefaultAsync(p => p.CaseId == caseId && p.Status == PrescriptionStatus.Active);



        if (prescription == null || prescription.PrescriptionItems.Count == 0)
        {
            throw new BusinessException("Không tìm thấy đơn thuốc hoặc đơn thuốc trống cho ca khám này.");
        }

        // Tạo Hóa đơn mới
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            CreatedAt = DateTime.UtcNow,
            Status = InvoiceStatus.PENDING,
            TotalAmount = 0
        };
        _context.Invoices.Add(invoice);

        decimal grandTotal = 0;

        // 3. Xử lý từng món thuốc (Greedy Allocation)
        foreach (var pItem in prescription.PrescriptionItems)
        {
            var remainingQuantity = pItem.QuantityBase;
            decimal volumePerBaseUnit = pItem.Medicine.VolumePerBaseUnit ?? 1m;
            if (remainingQuantity <= 0) continue;

            // Lấy tất cả các quy cách đóng gói được phép bán của loại thuốc này, xếp từ lớn xuống nhỏ
            var packagings = await _context.MedicinePackagings
                .Include(mp => mp.MedicineUnit)
                .Where(mp => mp.MedicineId == pItem.MedicineId && mp.IsSellable)
                .OrderByDescending(mp => mp.ConversionFactor)
                .ToListAsync();

            if (packagings.Count == 0)
            {
                throw new BusinessException($"Thuốc '{pItem.Medicine.Name}' chưa được cấu hình đơn vị bán (IsSellable = true).");
            }

            // Gom số lượng theo từng packaging trước, sau đó mới tạo InvoiceItem
            var billingLines = new List<(MedicinePackaging Pack, int Qty)>();

            foreach (var pack in packagings)
            {
                int packCapacityUS = (int)(pack.ConversionFactor * volumePerBaseUnit);

                if (remainingQuantity >= packCapacityUS)
                {
                    int qtyToBill = remainingQuantity / packCapacityUS;
                    remainingQuantity = remainingQuantity % packCapacityUS;
                    billingLines.Add((pack, qtyToBill));
                }
            }

            // Nếu vẫn còn lẻ, cộng thêm 1 vào đơn vị nhỏ nhất (không tạo dòng riêng)
            if (remainingQuantity > 0)
            {
                var smallestPack = packagings.Last();
                var idx = billingLines.FindIndex(b => b.Pack.Id == smallestPack.Id);
                if (idx >= 0)
                    billingLines[idx] = (billingLines[idx].Pack, billingLines[idx].Qty + 1);
                else
                    billingLines.Add((smallestPack, 1));
            }

            // Tạo InvoiceItem từ danh sách đã gộp
            foreach (var (pack, qty) in billingLines)
            {
                var invoiceItem = new InvoiceItem
                {
                    Id = Guid.NewGuid(),
                    InvoiceId = invoice.Id,
                    Description = $"{pItem.Medicine.Name} - {pack.MedicineUnit.Name}",
                    Quantity = qty,
                    UnitPrice = pack.SalePrice,
                    TotalPrice = qty * pack.SalePrice,
                    ReferenceId = pItem.PrescriptionItemId
                };
                _context.InvoiceItems.Add(invoiceItem);
                grandTotal += invoiceItem.TotalPrice;
            }
        }
        
        invoice.TotalAmount = grandTotal;
        
        await _context.SaveChangesAsync();

        // Send notification to all nurses
        var nurseIds = await _context.Users
            .Where(u => u.Role == UserRole.Staff)
            .Select(u => u.UserId)
            .ToListAsync();

        if (nurseIds.Count > 0)
        {
            var caseEntity = await _context.Cases
                .Include(c => c.PatientProfile)
                    .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(c => c.CaseId == caseId);
                
            var patientName = caseEntity?.PatientProfile?.User?.FullName ?? "bệnh nhân";

            await _notificationService.SendBulkAsync(nurseIds, new SendNotificationRequest
            {
                UserId = Guid.Empty, // Bỏ qua vì SendBulkAsync sẽ ghi đè
                Type = "new_invoice_created",
                Title = "Có hóa đơn mới",
                Body = $"Có hóa đơn mới của {patientName}. Vui lòng kiểm tra và thanh toán.",
                DeepLink = $"/invoices/{invoice.Id}",
                Metadata = new Dictionary<string, object>
                {
                    ["invoiceId"] = invoice.Id.ToString()
                }
            });
        }

        return invoice.Id;
    }

    public async Task<PagedResult<InvoiceResponse>> GetInvoicesAsync(InvoiceFilter filter)
    {
        var query = _context.Invoices
            .Include(i => i.Case)
                .ThenInclude(c => c.PatientProfile)
                    .ThenInclude(p => p.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(i => i.Id.ToString().Contains(search) || i.Case.PatientProfile.User.FullName.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<InvoiceStatus>(filter.Status, true, out var statusEnum))
        {
            query = query.Where(i => i.Status == statusEnum);
        }

        bool desc = string.Equals(filter.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? false : true;
        
        query = filter.SortBy?.ToLower() switch
        {
            "totalamount" => desc ? query.OrderByDescending(i => i.TotalAmount) : query.OrderBy(i => i.TotalAmount),
            "createdat" => desc ? query.OrderByDescending(i => i.CreatedAt) : query.OrderBy(i => i.CreatedAt),
            _ => desc ? query.OrderByDescending(i => i.CreatedAt) : query.OrderBy(i => i.CreatedAt)
        };

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(i => new InvoiceResponse
            {
                Id = i.Id,
                CaseId = i.CaseId,
                CaseName = i.Case.PatientProfile.User.FullName,
                TotalAmount = i.TotalAmount,
                CreatedAt = i.CreatedAt,
                PaidAt = i.PaidAt,
                Status = i.Status.ToString(),
                PaymentMethod = i.PaymentMethod != null ? i.PaymentMethod.ToString() : null
            })
            .ToListAsync();

        return new PagedResult<InvoiceResponse>(
            items, filter.Page, filter.PageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)filter.PageSize));
    }

    public async Task<InvoiceDetailResponse> GetInvoiceDetailAsync(Guid id)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Case)
                .ThenInclude(c => c.PatientProfile)
                    .ThenInclude(p => p.User)
            .Include(i => i.InvoiceItems)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null)
            throw new BusinessException("Không tìm thấy hóa đơn.");

        return new InvoiceDetailResponse
        {
            Id = invoice.Id,
            CaseId = invoice.CaseId,
            CaseName = invoice.Case.PatientProfile.User.FullName,
            TotalAmount = invoice.TotalAmount,
            CreatedAt = invoice.CreatedAt,
            PaidAt = invoice.PaidAt,
            Status = invoice.Status.ToString(),
            PaymentMethod = invoice.PaymentMethod != null ? invoice.PaymentMethod.ToString() : null,
            Items = invoice.InvoiceItems.Select(item => new InvoiceItemResponse
            {
                Id = item.Id,
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice
            }).ToList()
        };
    }

    public async Task PayAndDispenseAsync(Guid invoiceId, PaymentMethod method)
    {
        var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) throw new BusinessException("Không tìm thấy hóa đơn.");
        
        if (invoice.Status == InvoiceStatus.PAID)
            throw new BusinessException("Hóa đơn này đã được thanh toán.");

        // 1. Mark as PAID
        invoice.Status = InvoiceStatus.PAID;
        invoice.PaidAt = DateTime.UtcNow;
        invoice.PaymentMethod = method;

        // 2. Dispense items (FEFO, Inventory deduct)
        await _inventoryService.DispenseAsync(invoice.CaseId);

        // 3. Sinh MedicationIntakeLog sau khi đã xuất kho
        await GenerateIntakeLogsForPrescriptionAsync(invoice.CaseId);

        // Lưu trạng thái hóa đơn (giao dịch Inventory đã được add bên trong DispenseAsync)
        await _context.SaveChangesAsync();
    }

    private async Task GenerateIntakeLogsForPrescriptionAsync(Guid caseId)
    {
        var prescription = await _context.Prescriptions
            .Include(p => p.PrescriptionItems)
            .Include(p => p.Case)
            .FirstOrDefaultAsync(p => p.CaseId == caseId && p.Status == PrescriptionStatus.Active);

        if (prescription == null || prescription.PrescriptionItems.Count == 0) return;

        var patientPref = await _context.PatientReminderPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PatientProfileId == prescription.Case.PatientProfileId);

        var morningTime = patientPref?.MorningTime ?? new TimeOnly(7, 0);
        var middayTime  = patientPref?.MiddayTime ?? new TimeOnly(12, 0);
        var eveningTime = patientPref?.EveningTime ?? new TimeOnly(20, 0);

        var allLogs = new List<MedicationIntakeLog>();

        foreach (var pItem in prescription.PrescriptionItems)
        {
            var itemWithPatient = new PrescriptionItemWithPatient(
                pItem.PrescriptionItemId,
                prescription.Case.PatientProfileId,
                pItem.StartDate,
                pItem.DurationDays);

            // Chuyển mảng enum int về List<ScheduleSlot>
            var slots = pItem.ScheduleSlots?.Select(s => (ADSUS_BE.BLL.PrescriptionAdherence.DTOs.ScheduleSlot)(int)s).ToList() 
                ?? new List<ADSUS_BE.BLL.PrescriptionAdherence.DTOs.ScheduleSlot>();
            
            if (slots.Count == 0) continue;

            var scheduledDoses = await _scheduleGenerator.GenerateAsync(
                itemWithPatient,
                slots,
                morningTime,
                middayTime,
                eveningTime,
                DateTime.UtcNow);

            foreach (var dose in scheduledDoses)
            {
                allLogs.Add(new MedicationIntakeLog
                {
                    IntakeId = Guid.NewGuid(),
                    PrescriptionItemId = dose.PrescriptionItemId,
                    ScheduledTime = dose.ScheduledTimeUtc,
                    ConfirmedAt = null,
                });
            }
        }

        if (allLogs.Count > 0)
        {
            await _intakeLogRepo.AddRangeAsync(allLogs);
        }
    }

    public async Task CancelInvoiceAsync(Guid invoiceId, CancelInvoiceRequest request)
    {
        var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) throw new BusinessException("Không tìm thấy hóa đơn.");
        
        if (invoice.Status == InvoiceStatus.CANCELLED)
            throw new BusinessException("Hóa đơn này đã bị hủy từ trước.");

        if (invoice.Status == InvoiceStatus.PENDING)
        {
            invoice.Status = InvoiceStatus.CANCELLED;
            invoice.CancelledReason = request.Reason;
        }
        else if (invoice.Status == InvoiceStatus.PAID)
        {
            // Lấy danh sách PrescriptionItems của Case này
            var prescriptionItems = await _context.PrescriptionItems
                .Include(pi => pi.Prescription)
                .Where(pi => pi.Prescription.CaseId == invoice.CaseId)
                .ToListAsync();

            if (prescriptionItems.Count > 0)
            {
                var prescription = prescriptionItems.First().Prescription;
                prescription.Status = PrescriptionStatus.Cancelled;

                var itemIds = prescriptionItems.Select(p => p.PrescriptionItemId).ToList();

                var allLogs = await _context.MedicationIntakeLogs
                    .Where(l => itemIds.Contains(l.PrescriptionItemId))
                    .ToListAsync();

                var pendingLogs = allLogs.Where(l => l.ConfirmedAt == null).ToList();
                if (pendingLogs.Count > 0)
                {
                    _context.MedicationIntakeLogs.RemoveRange(pendingLogs);
                }

                var dispenseTransactions = await _context.InventoryTransactions
                    .Include(t => t.Batch)
                    .Where(t => t.TxnType == InventoryTxnType.Dispense 
                                && t.PrescriptionItemId.HasValue 
                                && itemIds.Contains(t.PrescriptionItemId.Value))
                    .ToListAsync();

                foreach (var pi in prescriptionItems)
                {
                    var itemLogs = allLogs.Where(l => l.PrescriptionItemId == pi.PrescriptionItemId).ToList();
                    var totalLogsCount = itemLogs.Count;
                    var pendingLogsCount = itemLogs.Count(l => l.ConfirmedAt == null);

                    if (totalLogsCount > 0 && pendingLogsCount == 0) continue; // Đã uống hết, không hoàn kho

                    var txnsForItem = dispenseTransactions.Where(t => t.PrescriptionItemId == pi.PrescriptionItemId).ToList();
                    
                    foreach (var txn in txnsForItem)
                    {
                        var batch = txn.Batch;
                        if (batch != null)
                        {
                            var refundQtyBase = totalLogsCount == 0 
                                ? txn.QuantityBase // Nếu chưa sinh log nào, hoàn lại toàn bộ
                                : (int)Math.Round((double)txn.QuantityBase * pendingLogsCount / totalLogsCount);
                            
                            if (refundQtyBase <= 0) continue;

                            batch.QuantityBase += refundQtyBase;

                            var reverseTxn = new InventoryTransaction
                            {
                                Id = Guid.NewGuid(),
                                BatchId = txn.BatchId,
                                MedicinePackagingId = txn.MedicinePackagingId,
                                TxnType = InventoryTxnType.Adjustment,
                                QuantityInUnit = refundQtyBase,
                                QuantityBase = refundQtyBase,
                                TxnDate = DateTime.UtcNow,
                                Reason = "Hoàn kho tự động do hủy hóa đơn",
                                PrescriptionItemId = txn.PrescriptionItemId
                            };
                            
                            _context.InventoryTransactions.Add(reverseTxn);
                        }
                    }
                }
            }

            invoice.Status = InvoiceStatus.CANCELLED;
            invoice.CancelledReason = request.Reason;
        }

        await _context.SaveChangesAsync();
    }
}
