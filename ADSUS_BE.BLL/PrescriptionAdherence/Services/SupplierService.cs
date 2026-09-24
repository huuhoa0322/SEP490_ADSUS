using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Services;

public class SupplierService : ISupplierService
{
    private readonly ISupplierRepository _suppliers;

    public SupplierService(ISupplierRepository suppliers)
    {
        _suppliers = suppliers;
    }

    public async Task<PagedResult<SupplierResponse>> GetSuppliersAsync(int pageIndex, int pageSize, string? search, CancellationToken ct = default)
    {
        var (suppliers, totalItems) = await _suppliers.SearchPagedAsync(search, pageIndex, pageSize, ct);

        var items = suppliers.Select(ToResponse).ToList();

        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        return new PagedResult<SupplierResponse>(items, pageIndex, pageSize, totalItems, totalPages);
    }

    public async Task<SupplierResponse> GetSupplierByIdAsync(Guid supplierId, CancellationToken ct = default)
    {
        var supplier = await _suppliers.GetByIdAsync(supplierId, ct);
        if (supplier == null)
            throw new ResourceNotFoundException($"Nhà cung cấp {supplierId} không tồn tại.");

        return ToResponse(supplier);
    }

    public async Task<SupplierResponse> CreateSupplierAsync(CreateSupplierRequest request, CancellationToken ct = default)
    {
        if (!Regex.IsMatch(request.PhoneNumber.Trim(), @"^0\d{9}$", RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            throw new BusinessException("Số điện thoại không hợp lệ. Số điện thoại phải bắt đầu bằng 0 và gồm đúng 10 chữ số.");
        }

        if (!Regex.IsMatch(request.TaxCode.Trim(), @"^\d{10}$|^\d{10}-\d{3}$", RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            throw new BusinessException("Mã số thuế phải là 10 chữ số hoặc 13 chữ số có dấu gạch ngang (VD: 1234567890 hoặc 1234567890-123).");
        }

        var existing = await _suppliers.FindDuplicateAsync(
            request.Name.Trim(),
            request.PhoneNumber.Trim(),
            request.Email.Trim(),
            request.TaxCode.Trim(),
            excludeSupplierId: null,
            ct);

        if (existing != null)
        {
            if (existing.Name.Equals(request.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new BusinessException("Tên nhà cung cấp đã tồn tại.");
            if (existing.PhoneNumber == request.PhoneNumber.Trim())
                throw new BusinessException("Số điện thoại nhà cung cấp đã tồn tại.");
            if (existing.Email.Equals(request.Email.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new BusinessException("Email nhà cung cấp đã tồn tại.");
            if (existing.TaxCode == request.TaxCode.Trim())
                throw new BusinessException("Mã số thuế nhà cung cấp đã tồn tại.");
        }

        var supplier = new Supplier
        {
            SupplierId = Guid.NewGuid(),
            Name = request.Name.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            Email = request.Email.Trim(),
            Address = request.Address.Trim(),
            TaxCode = request.TaxCode.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _suppliers.AddAsync(supplier, ct);
        await _suppliers.SaveChangesAsync(ct);

        return await GetSupplierByIdAsync(supplier.SupplierId, ct);
    }

    public async Task<SupplierResponse> UpdateSupplierAsync(Guid supplierId, UpdateSupplierRequest request, CancellationToken ct = default)
    {
        var supplier = await _suppliers.GetForUpdateAsync(supplierId, ct);
        if (supplier == null)
            throw new ResourceNotFoundException($"Nhà cung cấp {supplierId} không tồn tại.");

        if (!Regex.IsMatch(request.PhoneNumber.Trim(), @"^0\d{9}$", RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            throw new BusinessException("Số điện thoại không hợp lệ. Số điện thoại phải bắt đầu bằng 0 và gồm đúng 10 chữ số.");
        }

        // TaxCode cannot be updated — không kiểm tra trùng mã số thuế
        var existing = await _suppliers.FindDuplicateAsync(
            request.Name.Trim(),
            request.PhoneNumber.Trim(),
            request.Email.Trim(),
            taxCode: null,
            excludeSupplierId: supplierId,
            ct);

        if (existing != null)
        {
            if (existing.Name.Equals(request.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new BusinessException("Tên nhà cung cấp đã tồn tại.");
            if (existing.PhoneNumber == request.PhoneNumber.Trim())
                throw new BusinessException("Số điện thoại nhà cung cấp đã tồn tại.");
            if (existing.Email.Equals(request.Email.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new BusinessException("Email nhà cung cấp đã tồn tại.");
        }

        supplier.Name = request.Name.Trim();
        supplier.PhoneNumber = request.PhoneNumber.Trim();
        supplier.Email = request.Email.Trim();
        supplier.Address = request.Address.Trim();
        supplier.UpdatedAt = DateTime.UtcNow;

        await _suppliers.SaveChangesAsync(ct);

        return await GetSupplierByIdAsync(supplier.SupplierId, ct);
    }

    public async Task UpdateSupplierStatusAsync(Guid supplierId, bool isActive, CancellationToken ct = default)
    {
        var supplier = await _suppliers.GetForUpdateAsync(supplierId, ct);
        if (supplier == null)
            throw new ResourceNotFoundException($"Nhà cung cấp {supplierId} không tồn tại.");

        supplier.IsActive = isActive;
        supplier.UpdatedAt = DateTime.UtcNow;

        await _suppliers.SaveChangesAsync(ct);
    }

    private static SupplierResponse ToResponse(Supplier s) => new(
        s.SupplierId,
        s.Name,
        s.PhoneNumber,
        s.Email,
        s.Address,
        s.TaxCode,
        s.IsActive,
        s.CreatedAt,
        s.UpdatedAt);
}
