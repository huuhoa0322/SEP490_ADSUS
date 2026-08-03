using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;

/// <summary>
/// Module 7 UC-18 — tra cứu medicine catalog cho autocomplete khi bác sĩ kê đơn.
/// </summary>
public interface IMedicineService
{
    Task<IReadOnlyList<MedicineListItem>> SearchAsync(string keyword, CancellationToken ct = default);
}
