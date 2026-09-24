using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ADSUS_BE.BLL.CaseClinicServices;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs.Invoice;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.DAL.Repositories.Interfaces;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Services;

public class InvoiceService : IInvoiceService
{
    private readonly IInvoiceRepository _invoices;
    private readonly IPrescriptionRepository _prescriptions;
    private readonly IPrescriptionItemRepository _prescriptionItems;
    private readonly IInventoryRepository _inventory;
    private readonly IReminderPreferenceRepository _reminderPreferences;
    private readonly IUserRepository _users;
    private readonly ICaseClinicServiceService _caseClinicServices;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInventoryService _inventoryService;
    private readonly IMedicationIntakeLogRepository _intakeLogRepo;
    private readonly IMedicationIntakeScheduleGenerator _scheduleGenerator;
    private readonly INotificationService _notificationService;

    public InvoiceService(
        IInvoiceRepository invoices,
        IPrescriptionRepository prescriptions,
        IPrescriptionItemRepository prescriptionItems,
        IInventoryRepository inventory,
        IReminderPreferenceRepository reminderPreferences,
        IUserRepository users,
        ICaseClinicServiceService caseClinicServices,
        IUnitOfWork unitOfWork,
        IInventoryService inventoryService,
        IMedicationIntakeLogRepository intakeLogRepo,
        IMedicationIntakeScheduleGenerator scheduleGenerator,
        INotificationService notificationService)
    {
        _invoices = invoices;
        _prescriptions = prescriptions;
        _prescriptionItems = prescriptionItems;
        _inventory = inventory;
        _reminderPreferences = reminderPreferences;
        _users = users;
        _caseClinicServices = caseClinicServices;
        _unitOfWork = unitOfWork;
        _inventoryService = inventoryService;
        _intakeLogRepo = intakeLogRepo;
        _scheduleGenerator = scheduleGenerator;
        _notificationService = notificationService;
    }

    public async Task<Guid?> GenerateInvoiceIfBillableAsync(Guid caseId)
    {
        if (await _invoices.GetActiveIdByCaseAsync(caseId) is not null)
            return null;

        var hasServiceOrMedicine =
            (await _caseClinicServices.GetServicesForCaseAsync(caseId)).Count > 0
            || await _prescriptions.ExistsActiveByCaseAsync(caseId);

        return hasServiceOrMedicine ? await GenerateInvoiceForCaseAsync(caseId) : null;
    }

    public async Task<Guid> GenerateInvoiceForCaseAsync(Guid caseId)
    {
        // 1. Kiểm tra xem Case đã có hóa đơn nào PENDING/PAID chưa để tránh tạo trùng
        var existingInvoiceId = await _invoices.GetActiveIdByCaseAsync(caseId);
        if (existingInvoiceId is not null)
        {
            return existingInvoiceId.Value;
        }

        // 2. Lấy danh sách dịch vụ (module CaseClinicServices) và đơn thuốc của Case này
        var caseClinicServices = await _caseClinicServices.GetServicesForCaseAsync(caseId);

        var prescription = await _prescriptions.GetActiveByCaseWithItemsAsync(caseId);

        bool hasMedicine = prescription != null && prescription.PrescriptionItems.Count > 0;
        bool hasService = caseClinicServices.Count > 0;

        if (!hasMedicine && !hasService)
        {
            throw new BusinessException("Không tìm thấy dịch vụ hoặc đơn thuốc cho ca khám này (Không tìm thấy đơn thuốc).");
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
        await _invoices.AddAsync(invoice);

        decimal grandTotal = 0;

        // 3. Xử lý từng dịch vụ phòng khám
        foreach (var cs in caseClinicServices)
        {
            var serviceItem = new InvoiceItem
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                Description = string.IsNullOrEmpty(cs.ServiceName) ? "Dịch vụ phòng khám" : cs.ServiceName,
                Quantity = 1,
                UnitPrice = cs.PriceAtTime,
                TotalPrice = cs.PriceAtTime,
                ItemType = InvoiceItemType.Service,
                ReferenceId = cs.Id
            };
            await _invoices.AddItemAsync(serviceItem);
            grandTotal += serviceItem.TotalPrice;
        }

        // 4. Xử lý từng món thuốc (Greedy Allocation)
        if (hasMedicine && prescription != null)
        {
            foreach (var pItem in prescription.PrescriptionItems)
            {
                var remainingQuantity = pItem.QuantityBase;
                decimal volumePerBaseUnit = pItem.Medicine.VolumePerBaseUnit ?? 1m;
                if (remainingQuantity <= 0) continue;

                // Các quy cách được phép bán của thuốc (đã nạp sẵn cùng đơn), xếp từ lớn xuống nhỏ
                var packagings = pItem.Medicine.MedicinePackagings
                    .Where(mp => mp.IsSellable)
                    .OrderByDescending(mp => mp.ConversionFactor)
                    .ToList();

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
                        Description = $"Thuốc: {pItem.Medicine.Name} ({pack.MedicineUnit?.Name ?? "Đơn vị"})",
                        Quantity = qty,
                        UnitPrice = pack.SalePrice,
                        TotalPrice = qty * pack.SalePrice,
                        ItemType = InvoiceItemType.Medicine,
                        ReferenceId = pItem.PrescriptionItemId
                    };
                    await _invoices.AddItemAsync(invoiceItem);
                    grandTotal += invoiceItem.TotalPrice;
                }
            }
        }

        invoice.TotalAmount = grandTotal;
        await _unitOfWork.SaveChangesAsync();
        if (hasMedicine)
        {
            await _inventoryService.DispenseAsync(caseId);
        }

        // Send notification to all nurses
        var nurseIds = await _users.ListActiveUserIdsByRoleAsync(UserRole.Staff);

        if (nurseIds.Count > 0)
        {
            var patientName = await _invoices.GetPatientNameAsync(invoice.Id) ?? "bệnh nhân";

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

    private const int MaxInvoicePageSize = 100;

    public async Task<PagedResult<InvoiceResponse>> GetInvoicesAsync(InvoiceFilter filter)
    {
        // NFR-PER-03: giới hạn kích thước trang — trước đây filter.PageSize đi thẳng vào
        // Skip/Take không qua kiểm tra, 1 client gửi ?pageSize=999999 sẽ kéo cả bảng invoice.
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > MaxInvoicePageSize ? 10 : filter.PageSize;

        InvoiceStatus? status = !string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<InvoiceStatus>(filter.Status, true, out var statusEnum)
            ? statusEnum
            : null;

        bool desc = !string.Equals(filter.SortDir, "asc", StringComparison.OrdinalIgnoreCase);

        var (invoices, totalCount) = await _invoices.SearchPagedAsync(filter.Search, status, filter.SortBy, desc, page, pageSize);

        var items = invoices.Select(i => new InvoiceResponse
        {
            Id = i.Id,
            CaseId = i.CaseId,
            CaseName = i.Case?.PatientProfile?.User?.FullName ?? string.Empty,
            TotalAmount = i.TotalAmount,
            CreatedAt = i.CreatedAt,
            PaidAt = i.PaidAt,
            Status = i.Status.ToString(),
            PaymentMethod = i.PaymentMethod != null ? i.PaymentMethod.ToString() : null
        }).ToList();

        return new PagedResult<InvoiceResponse>(
            items, page, pageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<InvoiceDetailResponse> GetInvoiceDetailAsync(Guid id)
    {
        var invoice = await _invoices.GetDetailAsync(id);

        if (invoice == null)
            throw new BusinessException("Không tìm thấy hóa đơn.");

        var responseItems = new List<InvoiceItemResponse>();
        foreach (var item in invoice.InvoiceItems)
        {
            string unit = item.ItemType == InvoiceItemType.Service ? "Lần" : "";
            string desc = item.Description;

            if (item.ItemType == InvoiceItemType.Medicine)
            {
                var match = System.Text.RegularExpressions.Regex.Match(desc, @"\(([^)]+)\)$");
                if (match.Success)
                {
                    unit = match.Groups[1].Value;
                    desc = desc.Substring(0, match.Index).Trim();
                }

                if (desc.StartsWith("Thuốc: "))
                {
                    desc = desc.Substring("Thuốc: ".Length).Trim();
                }
            }

            responseItems.Add(new InvoiceItemResponse
            {
                Id = item.Id,
                Description = desc,
                Unit = unit,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice,
                ItemType = item.ItemType == InvoiceItemType.Service ? "SERVICE" : "MEDICINE"
            });
        }

        return new InvoiceDetailResponse
        {
            Id = invoice.Id,
            CaseId = invoice.CaseId,
            CaseName = invoice.Case?.PatientProfile?.User?.FullName ?? string.Empty,
            TotalAmount = invoice.TotalAmount,
            CreatedAt = invoice.CreatedAt,
            PaidAt = invoice.PaidAt,
            Status = invoice.Status.ToString(),
            PaymentMethod = invoice.PaymentMethod != null ? invoice.PaymentMethod.ToString() : null,
            Items = responseItems
        };
    }

    public async Task PayInvoiceAsync(Guid invoiceId, PaymentMethod method)
    {
        var invoice = await _invoices.GetWithItemsForUpdateAsync(invoiceId);
        if (invoice == null) throw new BusinessException("Không tìm thấy hóa đơn.");

        if (invoice.Status == InvoiceStatus.PAID)
            throw new BusinessException("Hóa đơn này đã được thanh toán.");

        // 1. Mark as PAID
        invoice.Status = InvoiceStatus.PAID;
        invoice.PaidAt = DateTime.UtcNow;
        invoice.PaymentMethod = method;

        bool hasMedicine = invoice.InvoiceItems.Any(i => i.ItemType == InvoiceItemType.Medicine);
        if (hasMedicine)
        {
            await GenerateIntakeLogsForPrescriptionAsync(invoice.CaseId);
        }

        // Lưu trạng thái hóa đơn (giao dịch Inventory đã được add bên trong DispenseAsync)
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task GenerateIntakeLogsForPrescriptionAsync(Guid caseId)
    {
        var prescription = await _prescriptions.GetActiveByCaseForIntakeScheduleAsync(caseId);

        if (prescription == null || prescription.PrescriptionItems.Count == 0) return;

        var patientPref = await _reminderPreferences.GetByPatientProfileIdAsync(prescription.Case.PatientProfileId);

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
        var invoice = await _invoices.GetForUpdateAsync(invoiceId);
        if (invoice == null) throw new BusinessException("Không tìm thấy hóa đơn.");

        if (invoice.Status == InvoiceStatus.CANCELLED)
            throw new BusinessException("Hóa đơn này đã bị hủy từ trước.");

        if (invoice.Status == InvoiceStatus.PENDING || invoice.Status == InvoiceStatus.PAID)
        {
            // Lấy danh sách PrescriptionItems của Case này
            var prescriptionItems = await _prescriptionItems.ListByCaseForUpdateAsync(invoice.CaseId);

            if (prescriptionItems.Count > 0)
            {
                var prescription = prescriptionItems[0].Prescription;
                prescription.Status = PrescriptionStatus.Cancelled;

                var itemIds = prescriptionItems.Select(p => p.PrescriptionItemId).ToList();

                var allLogs = await _intakeLogRepo.ListByItemIdsForUpdateAsync(itemIds);

                var pendingLogs = allLogs.Where(l => l.ConfirmedAt == null).ToList();
                if (pendingLogs.Count > 0)
                {
                    _intakeLogRepo.RemoveRange(pendingLogs);
                }

                var dispenseTransactions = await _inventory.ListDispenseTransactionsForUpdateAsync(itemIds);

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

                            if (invoice.Status == InvoiceStatus.PENDING)
                            {
                                // Xoá luôn giao dịch trừ kho ban đầu để tránh rác dữ liệu (đơn chưa thanh toán).
                                // Đơn PENDING thì chưa uống thuốc nên thực tế luôn hoàn đủ.
                                if (refundQtyBase == txn.QuantityBase)
                                {
                                    _inventory.RemoveTransaction(txn);
                                }
                                else
                                {
                                    txn.QuantityBase -= refundQtyBase;
                                    txn.QuantityInUnit -= refundQtyBase;
                                }
                            }
                            else
                            {
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

                                await _inventory.AddTransactionAsync(reverseTxn);
                            }
                        }
                    }
                }
            }

            invoice.Status = InvoiceStatus.CANCELLED;
            invoice.CancelledReason = request.Reason;
        }

        await _unitOfWork.SaveChangesAsync();
    }
}
