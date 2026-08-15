using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;

public interface IMedicineService
{
    Task<IEnumerable<MedicineResponse>> SearchMedicinesAsync(string keyword, int limit = 20, CancellationToken ct = default);
    Task<PagedResult<MedicineResponse>> GetPagedAsync(int page, int pageSize, string? keyword, CancellationToken ct = default);
    Task<MedicineResponse> CreateMedicineAsync(CreateMedicineRequest request, CancellationToken ct = default);
    Task<MedicineResponse> UpdateMedicineAsync(Guid id, UpdateMedicineRequest request, CancellationToken ct = default);
    Task DeleteMedicineAsync(Guid id, CancellationToken ct = default);
    Task ActivateMedicineAsync(Guid id, CancellationToken ct = default);
}

