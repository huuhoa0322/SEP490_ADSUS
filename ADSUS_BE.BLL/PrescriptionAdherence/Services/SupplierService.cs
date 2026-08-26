using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Services;

public class SupplierService : ISupplierService
{
    private readonly AppDbContext _context;

    public SupplierService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<SupplierResponse>> GetSuppliersAsync(int pageIndex, int pageSize, string? search, CancellationToken ct = default)
    {
        var query = _context.Suppliers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.ToLower().Trim();
            query = query.Where(s => s.Name.ToLower().Contains(keyword) || 
                                     s.PhoneNumber.Contains(keyword) ||
                                     s.TaxCode.Contains(keyword));
        }

        var totalItems = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SupplierResponse(
                s.SupplierId,
                s.Name,
                s.PhoneNumber,
                s.Email,
                s.Address,
                s.TaxCode,
                s.IsActive,
                s.CreatedAt,
                s.UpdatedAt))
            .ToListAsync(ct);

        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        return new PagedResult<SupplierResponse>(items, pageIndex, pageSize, totalItems, totalPages);
    }

    public async Task<SupplierResponse> GetSupplierByIdAsync(Guid supplierId, CancellationToken ct = default)
    {
        var supplier = await _context.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.SupplierId == supplierId, ct);
        if (supplier == null)
            throw new ResourceNotFoundException($"Nhà cung cấp {supplierId} không tồn tại.");

        return new SupplierResponse(
            supplier.SupplierId,
            supplier.Name,
            supplier.PhoneNumber,
            supplier.Email,
            supplier.Address,
            supplier.TaxCode,
            supplier.IsActive,
            supplier.CreatedAt,
            supplier.UpdatedAt
        );
    }

    public async Task<SupplierResponse> CreateSupplierAsync(CreateSupplierRequest request, CancellationToken ct = default)
    {
        if (!Regex.IsMatch(request.PhoneNumber.Trim(), @"^0\d{9}$"))
        {
            throw new BusinessException("Số điện thoại không hợp lệ. Số điện thoại phải bắt đầu bằng 0 và gồm đúng 10 chữ số.");
        }

        if (!Regex.IsMatch(request.TaxCode.Trim(), @"^\d{10}$|^\d{10}-\d{3}$"))
        {
            throw new BusinessException("Mã số thuế phải là 10 chữ số hoặc 13 chữ số có dấu gạch ngang (VD: 1234567890 hoặc 1234567890-123).");
        }

        var existing = await _context.Suppliers
            .Where(s => s.Name.ToLower() == request.Name.ToLower().Trim() ||
                        s.PhoneNumber == request.PhoneNumber.Trim() ||
                        s.Email.ToLower() == request.Email.ToLower().Trim() ||
                        s.TaxCode == request.TaxCode.Trim())
            .FirstOrDefaultAsync(ct);

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

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync(ct);

        return await GetSupplierByIdAsync(supplier.SupplierId, ct);
    }

    public async Task<SupplierResponse> UpdateSupplierAsync(Guid supplierId, UpdateSupplierRequest request, CancellationToken ct = default)
    {
        var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == supplierId, ct);
        if (supplier == null)
            throw new ResourceNotFoundException($"Nhà cung cấp {supplierId} không tồn tại.");

        if (!Regex.IsMatch(request.PhoneNumber.Trim(), @"^0\d{9}$"))
        {
            throw new BusinessException("Số điện thoại không hợp lệ. Số điện thoại phải bắt đầu bằng 0 và gồm đúng 10 chữ số.");
        }

        // TaxCode cannot be updated

        var existing = await _context.Suppliers
            .Where(s => s.SupplierId != supplierId && 
                        (s.Name.ToLower() == request.Name.ToLower().Trim() ||
                         s.PhoneNumber == request.PhoneNumber.Trim() ||
                         s.Email.ToLower() == request.Email.ToLower().Trim()))
            .FirstOrDefaultAsync(ct);

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

        await _context.SaveChangesAsync(ct);

        return await GetSupplierByIdAsync(supplier.SupplierId, ct);
    }

    public async Task UpdateSupplierStatusAsync(Guid supplierId, bool isActive, CancellationToken ct = default)
    {
        var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == supplierId, ct);
        if (supplier == null)
            throw new ResourceNotFoundException($"Nhà cung cấp {supplierId} không tồn tại.");

        supplier.IsActive = isActive;
        supplier.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
    }
}
