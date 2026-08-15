using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;

public interface IMedicineService
{
    Task<IEnumerable<MedicineResponse>> SearchMedicinesAsync(string keyword, int limit = 20, CancellationToken ct = default);
}
