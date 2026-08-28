using System;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Services;

public sealed class MedicineService : IMedicineService
{
    private readonly IMedicineRepository _medicineRepository;
    private readonly AppDbContext _db;

    public MedicineService(IMedicineRepository medicineRepository, AppDbContext db)
    {
        _medicineRepository = medicineRepository;
        _db = db;
    }

    public async Task<IEnumerable<MedicineResponse>> SearchMedicinesAsync(string keyword, int limit = 20, CancellationToken ct = default)
    {
        var medicines = await _medicineRepository.SearchByNameAsync(keyword, limit, ct);
        
        return medicines.Select(m => new MedicineResponse
        {
            MedicineId = m.MedicineId,
            Name = m.Name,
            Status = m.Status.ToString().ToUpperInvariant(),
            CreatedAt = m.CreatedAt,
            TotalInventoryBase = m.MedicineBatches?.Sum(b => b.QuantityBase) ?? 0
        });
    }

    public async Task<PagedResult<MedicineResponse>> GetPagedAsync(int page, int pageSize, string? keyword, bool? inStock = null, CancellationToken ct = default)
    {
        var (items, totalCount) = await _medicineRepository.GetPagedAsync(page, pageSize, keyword, inStock, ct);
        
        var medicineIds = items.Select(m => m.MedicineId).ToList();

        // Lấy tên đơn vị cơ bản cho từng thuốc (IsBaseUnit = true)
        var baseUnitNames = await _db.MedicinePackagings
            .Where(mp => medicineIds.Contains(mp.MedicineId) && mp.IsBaseUnit)
            .Select(mp => new { mp.MedicineId, mp.MedicineUnit.Name })
            .ToDictionaryAsync(x => x.MedicineId, x => x.Name, ct);
        
        var dtos = items.Select(m => new MedicineResponse
        {
            MedicineId = m.MedicineId,
            Name = m.Name,
            UsageUnit = m.UsageUnit,
            BaseUnitName = baseUnitNames.GetValueOrDefault(m.MedicineId),
            VolumePerBaseUnit = m.VolumePerBaseUnit,
            Status = m.Status.ToString().ToUpperInvariant(),
            CreatedAt = m.CreatedAt,
            TotalInventoryBase = m.MedicineBatches?.Sum(b => b.QuantityBase) ?? 0
        }).ToList();

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedResult<MedicineResponse>(dtos, page, pageSize, totalCount, totalPages);
    }

    public async Task<MedicineResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var m = await _db.Medicines
            .Include(x => x.MedicineBatches)
            .FirstOrDefaultAsync(x => x.MedicineId == id, ct);

        if (m == null) return null;

        var baseUnitName = await _db.MedicinePackagings
            .Where(mp => mp.MedicineId == id && mp.IsBaseUnit)
            .Select(mp => mp.MedicineUnit.Name)
            .FirstOrDefaultAsync(ct);

        return new MedicineResponse
        {
            MedicineId = m.MedicineId,
            Name = m.Name,
            UsageUnit = m.UsageUnit,
            BaseUnitName = baseUnitName,
            VolumePerBaseUnit = m.VolumePerBaseUnit,
            Status = m.Status.ToString().ToUpperInvariant(),
            CreatedAt = m.CreatedAt,
            TotalInventoryBase = m.MedicineBatches?.Sum(b => b.QuantityBase) ?? 0
        };
    }

    public async Task<MedicineResponse> CreateMedicineAsync(CreateMedicineRequest request, CancellationToken ct = default)
    {
        if (request.VolumePerBaseUnit > 0 && string.IsNullOrWhiteSpace(request.UsageUnit))
        {
            throw new BusinessException("Vui lòng nhập Đơn vị dùng (Usage Unit) khi đã nhập Hàm lượng.");
        }
        if (!string.IsNullOrWhiteSpace(request.UsageUnit) && (request.VolumePerBaseUnit == null || request.VolumePerBaseUnit <= 0))
        {
            throw new BusinessException("Vui lòng nhập đúng Hàm lượng (lớn hơn 0) khi đã nhập Đơn vị dùng.");
        }

        var existing = await _medicineRepository.FindByNameAsync(request.Name, ct);
        if (existing != null)
        {
            throw new BusinessException($"Thuốc với tên '{request.Name}' đã tồn tại.");
        }

        var medicine = new Medicine
        {
            MedicineId = Guid.NewGuid(),
            Name = request.Name.Trim(),
            UsageUnit = request.UsageUnit?.Trim(),
            VolumePerBaseUnit = request.VolumePerBaseUnit,
            CreatedAt = DateTime.UtcNow,
            Status = MedicineStatus.Active
        };

        await _medicineRepository.AddAsync(medicine, ct);
        
        var basePackaging = new MedicinePackaging
        {
            Id = Guid.NewGuid(),
            MedicineId = medicine.MedicineId,
            MedicineUnitId = request.MedicineUnitId,
            ConversionFactor = 1,
            IsBaseUnit = true,
            IsSellable = true,
            SalePrice = request.SalePrice
        };
        await _db.Set<MedicinePackaging>().AddAsync(basePackaging, ct);
        
        await _db.SaveChangesAsync(ct);

        return new MedicineResponse
        {
            MedicineId = medicine.MedicineId,
            Name = medicine.Name,
            UsageUnit = medicine.UsageUnit,
            VolumePerBaseUnit = medicine.VolumePerBaseUnit,
            Status = medicine.Status.ToString().ToUpperInvariant(),
            CreatedAt = medicine.CreatedAt
        };
    }

    public async Task<MedicineResponse> UpdateMedicineAsync(Guid id, UpdateMedicineRequest request, CancellationToken ct = default)
    {
        if (request.VolumePerBaseUnit > 0 && string.IsNullOrWhiteSpace(request.UsageUnit))
        {
            throw new BusinessException("Vui lòng nhập Đơn vị dùng (Usage Unit) khi đã nhập Hàm lượng.");
        }
        if (!string.IsNullOrWhiteSpace(request.UsageUnit) && (request.VolumePerBaseUnit == null || request.VolumePerBaseUnit <= 0))
        {
            throw new BusinessException("Vui lòng nhập đúng Hàm lượng (lớn hơn 0) khi đã nhập Đơn vị dùng.");
        }

        var existing = await _medicineRepository.GetByIdAsync(id, ct);
        if (existing == null)
        {
            throw new ResourceNotFoundException("Không tìm thấy thuốc.");
        }

        if (!string.Equals(existing.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("Tên thuốc là Master Data gốc, tuyệt đối không được sửa sau khi tạo.");
        }

        existing.UsageUnit = request.UsageUnit?.Trim();
        existing.VolumePerBaseUnit = request.VolumePerBaseUnit;

        await _medicineRepository.UpdateAsync(existing, ct);
        await _db.SaveChangesAsync(ct);

        return new MedicineResponse
        {
            MedicineId = existing.MedicineId,
            Name = existing.Name,
            UsageUnit = existing.UsageUnit,
            VolumePerBaseUnit = existing.VolumePerBaseUnit,
            Status = existing.Status.ToString().ToUpperInvariant(),
            CreatedAt = existing.CreatedAt
        };
    }

    public async Task DeleteMedicineAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await _medicineRepository.GetByIdAsync(id, ct);
        if (existing == null)
        {
            throw new ResourceNotFoundException("Kh�ng t�m th?y thu?c.");
        }

        // Soft delete
        existing.Status = MedicineStatus.Inactive;
        await _medicineRepository.UpdateAsync(existing, ct);
        await _db.SaveChangesAsync(ct);
    }
    public async Task ActivateMedicineAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await _medicineRepository.GetByIdAsync(id, ct);
        if (existing == null)
        {
            throw new ResourceNotFoundException("Kh�ng t�m th?y thu?c.");
        }

        existing.Status = MedicineStatus.Active;
        await _medicineRepository.UpdateAsync(existing, ct);
        await _db.SaveChangesAsync(ct);
    }
    public async Task<IEnumerable<MedicineUnitResponse>> GetMedicineUnitsAsync(CancellationToken ct = default)
    {
        var units = await _db.Set<MedicineUnit>()
            .OrderBy(u => u.Name)
            .Select(u => new MedicineUnitResponse
            {
                MedicineUnitId = u.MedicineUnitId,
                Name = u.Name
            })
            .ToListAsync(ct);
        return units;
    }

    public async Task<IEnumerable<MedicinePackagingResponse>> GetPackagingsByMedicineIdAsync(Guid medicineId, CancellationToken ct = default)
    {
        var packagings = await _db.Set<MedicinePackaging>()
            .Include(p => p.MedicineUnit)
            .Where(p => p.MedicineId == medicineId)
            .OrderByDescending(p => p.IsBaseUnit)
            .ThenBy(p => p.ConversionFactor)
            .Select(p => new MedicinePackagingResponse
            {
                Id = p.Id,
                MedicineId = p.MedicineId,
                MedicineUnitId = p.MedicineUnitId,
                UnitName = p.MedicineUnit.Name,
                ConversionFactor = p.ConversionFactor,
                IsBaseUnit = p.IsBaseUnit,
                SalePrice = p.SalePrice,
                IsSellable = p.IsSellable
            })
            .ToListAsync(ct);
        return packagings;
    }

    public async Task<MedicinePackagingResponse> AddPackagingAsync(Guid medicineId, CreateMedicinePackagingRequest request, CancellationToken ct = default)
    {
        var duplicateUnit = await _db.Set<MedicinePackaging>().FirstOrDefaultAsync(p => p.MedicineId == medicineId && p.MedicineUnitId == request.MedicineUnitId, ct);
        if (duplicateUnit != null)
        {
            throw new BusinessException("Đơn vị tính này đã được sử dụng cho thuốc. Không thể thêm trùng.");
        }

        if (request.IsBaseUnit)
        {
            throw new BusinessException("Thuốc đã có đơn vị cơ sở và không thể thiết lập thêm đơn vị cơ sở khác.");
        }

        var packaging = new MedicinePackaging
        {
            Id = Guid.NewGuid(),
            MedicineId = medicineId,
            MedicineUnitId = request.MedicineUnitId,
            ConversionFactor = request.ConversionFactor,
            IsBaseUnit = request.IsBaseUnit,
            SalePrice = request.SalePrice,
            IsSellable = request.IsSellable
        };

        await _db.Set<MedicinePackaging>().AddAsync(packaging, ct);
        await _db.SaveChangesAsync(ct);

        return await GetPackagingByIdAsync(packaging.Id, ct);
    }

    public async Task<MedicinePackagingResponse> UpdatePackagingAsync(Guid id, UpdateMedicinePackagingRequest request, CancellationToken ct = default)
    {
        var packaging = await _db.Set<MedicinePackaging>().FindAsync(new object[] { id }, ct);
        if (packaging == null) throw new ResourceNotFoundException("Không tìm thấy quy cách đóng gói.");

        if (packaging.IsBaseUnit)
        {
            if (!request.IsBaseUnit)
            {
                throw new BusinessException("Không thể gỡ bỏ trạng thái đơn vị cơ sở của quy cách này.");
            }
            if (request.MedicineUnitId != packaging.MedicineUnitId)
            {
                throw new BusinessException("Không thể thay đổi đơn vị tính của đơn vị cơ sở.");
            }
            if (request.ConversionFactor != 1)
            {
                throw new BusinessException("Hệ số quy đổi của đơn vị cơ sở luôn bằng 1.");
            }
        }
        else
        {
            if (request.MedicineUnitId != packaging.MedicineUnitId)
            {
                var duplicateUnit = await _db.Set<MedicinePackaging>().FirstOrDefaultAsync(p => p.MedicineId == packaging.MedicineId && p.MedicineUnitId == request.MedicineUnitId && p.Id != id, ct);
                if (duplicateUnit != null)
                {
                    throw new BusinessException("Đơn vị tính này đã được sử dụng bởi một quy cách khác của cùng loại thuốc.");
                }
            }

            if (request.IsBaseUnit)
            {
                throw new BusinessException("Không thể thay đổi đơn vị cơ sở của thuốc sau khi đã tạo.");
            }
        }

        packaging.MedicineUnitId = request.MedicineUnitId;
        packaging.ConversionFactor = request.ConversionFactor;
        packaging.IsBaseUnit = request.IsBaseUnit;
        packaging.SalePrice = request.SalePrice;
        packaging.IsSellable = request.IsSellable;

        _db.Set<MedicinePackaging>().Update(packaging);
        await _db.SaveChangesAsync(ct);

        return await GetPackagingByIdAsync(id, ct);
    }

    public async Task DeletePackagingAsync(Guid id, CancellationToken ct = default)
    {
        var packaging = await _db.Set<MedicinePackaging>().FindAsync(new object[] { id }, ct);
        if (packaging == null) throw new ResourceNotFoundException("Không tìm thấy quy cách đóng gói.");

        if (packaging.IsBaseUnit)
        {
            throw new BusinessException("Không thể xóa đơn vị cơ sở của thuốc.");
        }

        _db.Set<MedicinePackaging>().Remove(packaging);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<MedicinePackagingResponse> GetPackagingByIdAsync(Guid id, CancellationToken ct)
    {
        return await _db.Set<MedicinePackaging>()
            .Include(p => p.MedicineUnit)
            .Where(p => p.Id == id)
            .Select(p => new MedicinePackagingResponse
            {
                Id = p.Id,
                MedicineId = p.MedicineId,
                MedicineUnitId = p.MedicineUnitId,
                UnitName = p.MedicineUnit.Name,
                ConversionFactor = p.ConversionFactor,
                IsBaseUnit = p.IsBaseUnit,
                SalePrice = p.SalePrice,
                IsSellable = p.IsSellable
            }).FirstAsync(ct);
    }
}