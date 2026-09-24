using ADSUS_BE.BLL.CaseClinicServices.DTOs;
using ADSUS_BE.BLL.ClinicServiceManagement;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.CaseClinicServices;

/// <summary>
/// Gắn/gỡ dịch vụ phòng khám vào ca khám. Dữ liệu module khác đi qua service sở hữu: ca khám và
/// ảnh siêu âm qua ICaseService, danh mục dịch vụ qua IClinicServiceManagementService, hoá đơn qua
/// IInvoiceService (P11 review 24/09/2026).
///
/// ICaseService và IInvoiceService được inject dạng Lazy vì hai service đó lại phụ thuộc ngược vào
/// ICaseClinicServiceService (CaseService tự gắn "Khám thường", InvoiceService đọc dịch vụ để lập
/// hoá đơn) — inject trực tiếp thì DI không dựng được vòng này.
/// </summary>
public class CaseClinicServiceService : ICaseClinicServiceService
{
    private readonly ICaseClinicServiceRepository _caseServices;
    private readonly IClinicServiceManagementService _clinicServices;
    private readonly Lazy<ICaseService> _cases;
    private readonly Lazy<IInvoiceService> _invoices;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CaseClinicServiceService> _logger;

    public CaseClinicServiceService(
        ICaseClinicServiceRepository caseServices,
        IClinicServiceManagementService clinicServices,
        Lazy<ICaseService> cases,
        Lazy<IInvoiceService> invoices,
        IUnitOfWork unitOfWork,
        ILogger<CaseClinicServiceService> logger)
    {
        _caseServices = caseServices;
        _clinicServices = clinicServices;
        _cases = cases;
        _invoices = invoices;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CaseClinicServiceResponse>> GetServicesForCaseAsync(Guid caseId, CancellationToken ct = default)
    {
        var services = await _caseServices.ListByCaseAsync(caseId, ct);

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

    public Task<CaseClinicServiceResponse> AddServiceToCaseAsync(Guid caseId, Guid clinicServiceId, CancellationToken ct = default)
    {
        return AddServiceToCaseInternalAsync(caseId, clinicServiceId, allowBooked: false, actingDoctorId: null, ct: ct);
    }

    public Task<CaseClinicServiceResponse> AddServiceToCaseAsync(Guid caseId, Guid clinicServiceId, Guid actingDoctorId, CancellationToken ct = default)
    {
        return AddServiceToCaseInternalAsync(caseId, clinicServiceId, allowBooked: false, actingDoctorId: actingDoctorId, ct: ct);
    }

    private async Task<CaseClinicServiceResponse> AddServiceToCaseInternalAsync(Guid caseId, Guid clinicServiceId, bool allowBooked, Guid? actingDoctorId = null, CancellationToken ct = default)
    {
        if (await _invoices.Value.HasPaidInvoiceAsync(caseId, ct))
            throw new BusinessException("Hóa đơn đã thanh toán, không thể thêm dịch vụ vào ca khám.");

        var medicalCase = await _cases.Value.FindOwnershipAsync(caseId, ct);
        if (medicalCase == null)
        {
            throw new BusinessException("Không tìm thấy ca khám.");
        }

        if (actingDoctorId.HasValue && medicalCase.DoctorId != actingDoctorId.Value)
        {
            throw new BusinessException("Chỉ bác sĩ phụ trách ca khám mới có quyền chọn dịch vụ khám.");
        }

        if (medicalCase.Status == CaseStatus.Booked && !allowBooked)
        {
            throw new BusinessException("Không thể thêm dịch vụ vào ca khám chưa check-in.");
        }

        if (medicalCase.Status == CaseStatus.End || medicalCase.Status == CaseStatus.Cancelled)
        {
            throw new BusinessException("Không thể thêm dịch vụ vào ca khám đã hoàn thành hoặc bị hủy.");
        }

        var service = await _clinicServices.FindByIdAsync(clinicServiceId, ct);
        if (service == null || !service.IsActive)
        {
            throw new BusinessException("Dịch vụ không tồn tại hoặc không hoạt động.");
        }

        var existing = await _caseServices.GetByCaseAndServiceAsync(caseId, clinicServiceId, ct);

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

        await _caseServices.AddAsync(caseClinicService, ct);

        // Hoá đơn PENDING (nếu có) thêm dòng dịch vụ — lưu cùng lượt với bản ghi dịch vụ
        await _invoices.Value.StageServiceAddedAsync(caseId, caseClinicService.Id, service.Name, caseClinicService.PriceAtTime, ct);

        await _unitOfWork.SaveChangesAsync(ct);

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
        var service = await _clinicServices.FindActiveByCodeAsync(serviceCode, ct);

        if (service == null)
        {
            _logger.LogWarning("Không tìm thấy dịch vụ hoặc dịch vụ không hoạt động với mã {ServiceCode} để gắn vào ca khám {CaseId}", serviceCode, caseId);
            return;
        }

        await AddServiceToCaseInternalAsync(caseId, service.Id, allowBooked: true, actingDoctorId: null, ct: ct);
    }

    public Task RemoveServiceFromCaseAsync(Guid caseId, Guid caseClinicServiceId, CancellationToken ct = default)
    {
        return RemoveServiceFromCaseInternalAsync(caseId, caseClinicServiceId, actingDoctorId: null, ct: ct);
    }

    public Task RemoveServiceFromCaseAsync(Guid caseId, Guid caseClinicServiceId, Guid actingDoctorId, CancellationToken ct = default)
    {
        return RemoveServiceFromCaseInternalAsync(caseId, caseClinicServiceId, actingDoctorId: actingDoctorId, ct: ct);
    }

    private async Task RemoveServiceFromCaseInternalAsync(Guid caseId, Guid caseClinicServiceId, Guid? actingDoctorId, CancellationToken ct)
    {
        var record = await _caseServices.GetForUpdateAsync(caseClinicServiceId, ct);

        if (record == null || record.CaseId != caseId)
        {
            throw new BusinessException("Không tìm thấy dịch vụ.");
        }

        var medicalCase = await _cases.Value.FindOwnershipAsync(record.CaseId, ct);

        if (actingDoctorId.HasValue && medicalCase?.DoctorId != actingDoctorId.Value)
        {
            throw new BusinessException("Chỉ bác sĩ phụ trách ca khám mới có quyền xóa dịch vụ khám.");
        }

        if (medicalCase?.Status == CaseStatus.Booked)
        {
            throw new BusinessException("Không thể xóa dịch vụ khỏi ca khám chưa check-in.");
        }

        if (medicalCase?.Status == CaseStatus.End || medicalCase?.Status == CaseStatus.Cancelled)
        {
            throw new BusinessException("Không thể xóa dịch vụ khỏi ca khám đã hoàn thành hoặc bị hủy.");
        }

        var serviceCode = record.ClinicService?.Code?.Trim();
        if (string.IsNullOrWhiteSpace(serviceCode))
        {
            throw new InvalidOperationException("ClinicService not loaded or missing code for CaseClinicService.");
        }

        if (string.Equals(serviceCode, "GENERAL_EXAM", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("Không thể xóa dịch vụ khám thường.");
        }

        if (string.Equals(serviceCode, "ULTRASOUND_EXAM", StringComparison.OrdinalIgnoreCase)
            && await _cases.Value.HasUltrasoundImagesAsync(caseId, ct))
        {
            throw new BusinessException("Không thể xóa dịch vụ siêu âm khi ca khám đã có ảnh siêu âm.");
        }

        if (await _invoices.Value.HasPaidInvoiceAsync(caseId, ct))
        {
            throw new BusinessException("Không thể xóa dịch vụ khi hóa đơn đã thanh toán.");
        }

        // Hoá đơn PENDING (nếu có) bỏ dòng dịch vụ — lưu cùng lượt với việc gỡ dịch vụ
        await _invoices.Value.StageServiceRemovedAsync(caseId, caseClinicServiceId, ct);

        _caseServices.Remove(record);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Đã xóa dịch vụ {CaseClinicServiceId} khỏi ca khám {CaseId}", caseClinicServiceId, caseId);
    }
}
