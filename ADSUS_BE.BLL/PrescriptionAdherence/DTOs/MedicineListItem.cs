namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

/// <summary>
/// Module 7 UC-18 — item trong dropdown autocomplete thuốc.
/// </summary>
public sealed record MedicineListItem(Guid MedicineId, string Name);
