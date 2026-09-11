using ADSUS_BE.BLL.ClinicServiceManagement.DTOs;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.ClinicServiceManagement;

public class ClinicServiceManagementService : IClinicServiceManagementService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ClinicServiceManagementService> _logger;

    public ClinicServiceManagementService(
        AppDbContext context,
        ILogger<ClinicServiceManagementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ClinicServiceResponse>> GetAllAsync(bool? isActive, CancellationToken ct = default)
    {
        var query = _context.ClinicServices.AsNoTracking();

        if (isActive.HasValue)
        {
            query = query.Where(s => s.IsActive == isActive.Value);
        }

        var list = await query.OrderBy(s => s.Code).ToListAsync(ct);
        return list.Select(MapToResponse).ToList();
    }

    public async Task<ClinicServiceResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var service = await _context.ClinicServices.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (service == null)
        {
            throw new BusinessException("Không tìm thấy dịch vụ.");
        }

        return MapToResponse(service);
    }

    public async Task<ClinicServiceResponse> CreateAsync(CreateClinicServiceRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new ValidationException(new[] { new ValidationFailure(nameof(request.Code), "Mã dịch vụ không được để trống.") });
        }

        if (request.Code.Length > 50)
        {
            throw new ValidationException(new[] { new ValidationFailure(nameof(request.Code), "Mã dịch vụ không được vượt quá 50 ký tự.") });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException(new[] { new ValidationFailure(nameof(request.Name), "Tên dịch vụ không được để trống.") });
        }

        if (request.Name.Length > 200)
        {
            throw new ValidationException(new[] { new ValidationFailure(nameof(request.Name), "Tên dịch vụ không được vượt quá 200 ký tự.") });
        }

        if (request.Price <= 0)
        {
            throw new ValidationException(new[] { new ValidationFailure(nameof(request.Price), "Giá dịch vụ phải lớn hơn 0.") });
        }

        var codeNormalized = request.Code.Trim().ToUpper();
        var exists = await _context.ClinicServices.AnyAsync(s => s.Code.ToUpper() == codeNormalized, ct);
        if (exists)
        {
            throw new ConflictException("Mã dịch vụ đã tồn tại.");
        }

        var now = DateTime.UtcNow;
        var service = new ClinicService
        {
            Id = Guid.NewGuid(),
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Price = request.Price,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.ClinicServices.Add(service);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Đã tạo dịch vụ phòng khám {Code} ({Name}) với giá {Price}", service.Code, service.Name, service.Price);
        return MapToResponse(service);
    }

    public async Task<ClinicServiceResponse> UpdateAsync(Guid id, UpdateClinicServiceRequest request, CancellationToken ct = default)
    {
        var service = await _context.ClinicServices.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (service == null)
        {
            throw new BusinessException("Không tìm thấy dịch vụ.");
        }

        if (request.Price.HasValue && request.Price.Value <= 0)
        {
            throw new ValidationException(new[] { new ValidationFailure(nameof(request.Price), "Giá dịch vụ phải lớn hơn 0.") });
        }

        if (request.Name != null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ValidationException(new[] { new ValidationFailure(nameof(request.Name), "Tên dịch vụ không được để trống.") });
            }

            if (request.Name.Length > 200)
            {
                throw new ValidationException(new[] { new ValidationFailure(nameof(request.Name), "Tên dịch vụ không được vượt quá 200 ký tự.") });
            }

            service.Name = request.Name.Trim();
        }

        if (request.Description != null)
        {
            service.Description = request.Description.Trim();
        }

        if (request.Price.HasValue)
        {
            service.Price = request.Price.Value;
        }

        if (request.IsActive.HasValue)
        {
            service.IsActive = request.IsActive.Value;
        }

        service.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Đã cập nhật dịch vụ phòng khám {ServiceId} ({Code})", service.Id, service.Code);
        return MapToResponse(service);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var service = await _context.ClinicServices.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (service == null)
        {
            throw new BusinessException("Không tìm thấy dịch vụ.");
        }

        if (!service.IsActive)
        {
            // Idempotent soft delete
            return;
        }

        service.IsActive = false;
        service.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Đã vô hiệu hóa (soft delete) dịch vụ phòng khám {ServiceId} ({Code})", service.Id, service.Code);
    }

    private static ClinicServiceResponse MapToResponse(ClinicService service)
    {
        return new ClinicServiceResponse
        {
            Id = service.Id,
            Code = service.Code,
            Name = service.Name,
            Description = service.Description,
            Price = service.Price,
            IsActive = service.IsActive,
            CreatedAt = service.CreatedAt,
            UpdatedAt = service.UpdatedAt
        };
    }
}
