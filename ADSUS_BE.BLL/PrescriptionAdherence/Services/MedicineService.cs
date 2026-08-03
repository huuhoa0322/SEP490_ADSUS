using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.DAL.Repositories.Interfaces;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Services;

/// <summary>
/// Module 7 UC-18 BR-01 — autocomplete thuốc trong danh mục cho bác sĩ khi kê đơn.
/// </summary>
public sealed class MedicineService : IMedicineService
{
    private readonly IMedicineRepository _medicines;

    public MedicineService(IMedicineRepository medicines) => _medicines = medicines;

    public async Task<IReadOnlyList<MedicineListItem>> SearchAsync(string keyword, CancellationToken ct = default)
    {
        var matches = await _medicines.SearchAsync(keyword, 20, ct);
        return matches.Select(m => new MedicineListItem(m.MedicineId, m.Name)).ToList();
    }
}
