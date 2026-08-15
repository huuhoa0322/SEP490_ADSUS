using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.DAL.Repositories.Interfaces;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Services;

public sealed class MedicineService : IMedicineService
{
    private readonly IMedicineRepository _medicineRepository;

    public MedicineService(IMedicineRepository medicineRepository)
    {
        _medicineRepository = medicineRepository;
    }

    public async Task<IEnumerable<MedicineResponse>> SearchMedicinesAsync(string keyword, int limit = 20, CancellationToken ct = default)
    {
        var medicines = await _medicineRepository.SearchByNameAsync(keyword, limit, ct);
        
        return medicines.Select(m => new MedicineResponse
        {
            MedicineId = m.MedicineId,
            Name = m.Name
        });
    }
}
