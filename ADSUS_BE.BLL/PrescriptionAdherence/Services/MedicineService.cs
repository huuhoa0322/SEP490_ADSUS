using System;
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
            throw new BusinessException($"Thu?c v?i tên '{request.Name}' dã t?n t?i.");
        }

        var medicine = new Medicine
        {
            MedicineId = Guid.NewGuid(),
            Name = request.Name.Trim(),
            CreatedAt = DateTime.UtcNow,
            Status = MedicineStatus.Active
        };

        await _medicineRepository.AddAsync(medicine, ct);
        await _db.SaveChangesAsync(ct);

        return new MedicineResponse
        {
            MedicineId = medicine.MedicineId,
            Name = medicine.Name,
            Status = medicine.Status.ToString().ToUpperInvariant(),
            CreatedAt = medicine.CreatedAt
        };
    }

    public async Task<MedicineResponse> UpdateMedicineAsync(Guid id, UpdateMedicineRequest request, CancellationToken ct = default)
    {
        var existing = await _medicineRepository.GetByIdAsync(id, ct);
        if (existing == null)
        {
            throw new ResourceNotFoundException("Không tìm th?y thu?c.");
        }

        var nameCheck = await _medicineRepository.FindByNameAsync(request.Name, ct);
        if (nameCheck != null && nameCheck.MedicineId != id)
        {
            throw new BusinessException($"Thu?c v?i tên '{request.Name}' dã t?n t?i.");
        }

        existing.Name = request.Name.Trim();
        await _medicineRepository.UpdateAsync(existing, ct);
        await _db.SaveChangesAsync(ct);

        return new MedicineResponse
        {
            MedicineId = existing.MedicineId,
            Name = existing.Name,
            Status = existing.Status.ToString().ToUpperInvariant(),
            CreatedAt = existing.CreatedAt
        };
    }

    public async Task DeleteMedicineAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await _medicineRepository.GetByIdAsync(id, ct);
        if (existing == null)
        {
            throw new ResourceNotFoundException("Không tìm th?y thu?c.");
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
            throw new ResourceNotFoundException("Không tìm th?y thu?c.");
        }

        existing.Status = MedicineStatus.Active;
        await _medicineRepository.UpdateAsync(existing, ct);
        await _db.SaveChangesAsync(ct);
    }
}
