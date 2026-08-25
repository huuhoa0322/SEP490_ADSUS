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
            CreatedAt = m.CreatedAt
        });
    }

    public async Task<PagedResult<MedicineResponse>> GetPagedAsync(int page, int pageSize, string? keyword, CancellationToken ct = default)
    {
        var (items, totalCount) = await _medicineRepository.GetPagedAsync(page, pageSize, keyword, ct);
        
        var dtos = items.Select(m => new MedicineResponse
        {
            MedicineId = m.MedicineId,
            Name = m.Name,
            UsageUnit = m.UsageUnit,
            VolumePerBaseUnit = m.VolumePerBaseUnit,
            Status = m.Status.ToString().ToUpperInvariant(),
            CreatedAt = m.CreatedAt
        }).ToList();

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedResult<MedicineResponse>(dtos, page, pageSize, totalCount, totalPages);
    }

    public async Task<MedicineResponse> CreateMedicineAsync(CreateMedicineRequest request, CancellationToken ct = default)
    {
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
        var existing = await _medicineRepository.GetByIdAsync(id, ct);
        if (existing == null)
        {
            throw new ResourceNotFoundException("Không tìm thấy thuốc.");
        }

        var nameCheck = await _medicineRepository.FindByNameAsync(request.Name, ct);
        if (nameCheck != null && nameCheck.MedicineId != id)
        {
            throw new BusinessException($"Thuốc với tên '{request.Name}' đã tồn tại.");
        }

        existing.Name = request.Name.Trim();
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
        if (request.IsBaseUnit)
        {
            var existingBase = await _db.Set<MedicinePackaging>().FirstOrDefaultAsync(p => p.MedicineId == medicineId && p.IsBaseUnit, ct);
            if (existingBase != null)
            {
                existingBase.IsBaseUnit = false;
                _db.Set<MedicinePackaging>().Update(existingBase);
            }
            request.ConversionFactor = 1;
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
        if (packaging == null) throw new ResourceNotFoundException("Khong tim thay quy cach dong goi.");

        if (request.IsBaseUnit && !packaging.IsBaseUnit)
        {
            var existingBase = await _db.Set<MedicinePackaging>().FirstOrDefaultAsync(p => p.MedicineId == packaging.MedicineId && p.IsBaseUnit && p.Id != id, ct);
            if (existingBase != null)
            {
                existingBase.IsBaseUnit = false;
                _db.Set<MedicinePackaging>().Update(existingBase);
            }
            request.ConversionFactor = 1;
        }

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
        if (packaging == null) throw new ResourceNotFoundException("Khong tim thay quy cach dong goi.");

        if (packaging.IsBaseUnit)
        {
            throw new BusinessException("Khong the xoa don vi co so. Vui long chon don vi khac lam don vi co so truoc khi xoa.");
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