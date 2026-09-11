using ADSUS_BE.BLL.CaseClinicServices.DTOs;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.CaseClinicServices;

public class CaseClinicServiceService : ICaseClinicServiceService
{
    private readonly AppDbContext _context;
    private readonly ILogger<CaseClinicServiceService> _logger;

    public CaseClinicServiceService(
        AppDbContext context,
        ILogger<CaseClinicServiceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CaseClinicServiceResponse>> GetServicesForCaseAsync(Guid caseId, CancellationToken ct = default)
    {
        var services = await _context.CaseClinicServices
            .AsNoTracking()
            .Include(cs => cs.ClinicService)
            .Where(cs => cs.CaseId == caseId)
            .OrderBy(cs => cs.CreatedAt)
            .ToListAsync(ct);

        return services.Select(cs => new CaseClinicServiceResponse
        {
            Id = cs.Id,
            CaseId = cs.CaseId,
            ClinicServiceId = cs.ClinicServiceId,
            ServiceName = cs.ClinicService?.Name ?? string.Empty,
            ServiceCode = cs.ClinicService?.Code ?? string.Empty,
            PriceAtTime = cs.PriceAtTime,
            CreatedAt = cs.CreatedAt
        }).ToList();
    }

    public async Task<CaseClinicServiceResponse> AddServiceToCaseAsync(Guid caseId, Guid clinicServiceId, CancellationToken ct = default)
    {
        var medicalCase = await _context.Cases.FirstOrDefaultAsync(c => c.CaseId == caseId, ct);
        if (medicalCase == null)
        {
            throw new BusinessException("Không tìm thấy ca khám.");
        }

        if (medicalCase.Status == CaseStatus.End || medicalCase.Status == CaseStatus.Cancelled)
        {
            throw new BusinessException("Không thể thêm dịch vụ vào ca khám đã hoàn thành hoặc bị hủy.");
        }

        var service = await _context.ClinicServices.FirstOrDefaultAsync(s => s.Id == clinicServiceId, ct);
        if (service == null || !service.IsActive)
        {
            throw new BusinessException("Dịch vụ không tồn tại hoặc không hoạt động.");
        }

        var existing = await _context.CaseClinicServices
            .Include(cs => cs.ClinicService)
            .FirstOrDefaultAsync(cs => cs.CaseId == caseId && cs.ClinicServiceId == clinicServiceId, ct);

        if (existing != null)
        {
            return new CaseClinicServiceResponse
            {
                Id = existing.Id,
                CaseId = existing.CaseId,
                ClinicServiceId = existing.ClinicServiceId,
                ServiceName = existing.ClinicService?.Name ?? service.Name,
                ServiceCode = existing.ClinicService?.Code ?? service.Code,
                PriceAtTime = existing.PriceAtTime,
                CreatedAt = existing.CreatedAt
            };
        }

        var now = DateTime.UtcNow;
        var caseClinicService = new CaseClinicService
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            ClinicServiceId = clinicServiceId,
            PriceAtTime = service.Price,
            CreatedAt = now
        };

        _context.CaseClinicServices.Add(caseClinicService);

        var pendingInvoice = await _context.Invoices
            .Include(i => i.InvoiceItems)
            .FirstOrDefaultAsync(i => i.CaseId == caseId && i.Status == InvoiceStatus.PENDING, ct);

        if (pendingInvoice != null)
        {
            var invoiceItem = new InvoiceItem
            {
                Id = Guid.NewGuid(),
                InvoiceId = pendingInvoice.Id,
                Description = service.Name,
                Quantity = 1,
                UnitPrice = caseClinicService.PriceAtTime,
                TotalPrice = caseClinicService.PriceAtTime,
                ItemType = InvoiceItemType.Service,
                ReferenceId = caseClinicService.Id
            };

            _context.InvoiceItems.Add(invoiceItem);
            pendingInvoice.InvoiceItems.Add(invoiceItem);
            pendingInvoice.TotalAmount += invoiceItem.TotalPrice;
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Đã gắn dịch vụ {ServiceName} ({Price}) vào ca khám {CaseId}", service.Name, caseClinicService.PriceAtTime, caseId);

        return new CaseClinicServiceResponse
        {
            Id = caseClinicService.Id,
            CaseId = caseClinicService.CaseId,
            ClinicServiceId = caseClinicService.ClinicServiceId,
            ServiceName = service.Name,
            ServiceCode = service.Code,
            PriceAtTime = caseClinicService.PriceAtTime,
            CreatedAt = caseClinicService.CreatedAt
        };
    }

    public async Task AddServiceToCaseByCodeAsync(Guid caseId, string serviceCode, CancellationToken ct = default)
    {
        var service = await _context.ClinicServices
            .FirstOrDefaultAsync(s => s.Code == serviceCode && s.IsActive, ct);

        if (service == null)
        {
            _logger.LogWarning("Không tìm thấy dịch vụ hoặc dịch vụ không hoạt động với mã {ServiceCode} để gắn vào ca khám {CaseId}", serviceCode, caseId);
            return;
        }

        await AddServiceToCaseAsync(caseId, service.Id, ct);
    }

    public async Task RemoveServiceFromCaseAsync(Guid caseId, Guid caseClinicServiceId, CancellationToken ct = default)
    {
        var record = await _context.CaseClinicServices
            .FirstOrDefaultAsync(cs => cs.Id == caseClinicServiceId, ct);

        if (record == null || record.CaseId != caseId)
        {
            throw new BusinessException("Không tìm thấy dịch vụ.");
        }

        var invoices = await _context.Invoices
            .Include(i => i.InvoiceItems)
            .Where(i => i.CaseId == caseId)
            .ToListAsync(ct);

        if (invoices.Any(i => i.Status == InvoiceStatus.PAID))
        {
            throw new BusinessException("Không thể xóa dịch vụ khi hóa đơn đã thanh toán.");
        }

        var pendingInvoice = invoices.FirstOrDefault(i => i.Status == InvoiceStatus.PENDING);
        if (pendingInvoice != null)
        {
            var itemToRemove = pendingInvoice.InvoiceItems
                .FirstOrDefault(item => item.ReferenceId == caseClinicServiceId && item.ItemType == InvoiceItemType.Service);

            if (itemToRemove != null)
            {
                _context.InvoiceItems.Remove(itemToRemove);
                pendingInvoice.InvoiceItems.Remove(itemToRemove);
                pendingInvoice.TotalAmount -= itemToRemove.TotalPrice;

                if (pendingInvoice.InvoiceItems.Count == 0)
                {
                    pendingInvoice.Status = InvoiceStatus.CANCELLED;
                    pendingInvoice.CancelledReason = "Tự động hủy do đã xóa hết dịch vụ";
                }
            }
        }

        _context.CaseClinicServices.Remove(record);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Đã xóa dịch vụ {CaseClinicServiceId} khỏi ca khám {CaseId}", caseClinicServiceId, caseId);
    }
}
