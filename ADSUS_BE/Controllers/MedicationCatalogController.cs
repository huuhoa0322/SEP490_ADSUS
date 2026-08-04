using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

[ApiController]
[Route("api/v1/medication-catalog")]
public class MedicationCatalogController : ControllerBase
{
    private readonly IMedicineRepository _medicineRepo;

    public MedicationCatalogController(IMedicineRepository medicineRepo)
    {
        _medicineRepo = medicineRepo;
    }

    /// <summary>Danh mục thuốc — công khai (dùng cho bác sĩ khi kê đơn).</summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<MedicationCatalogItem>>> ListCatalog(
        CancellationToken ct)
    {
        var medicines = await _medicineRepo.ListAllAsync(ct);
        var result = medicines.Select(m => new MedicationCatalogItem(
            m.MedicineId,
            m.Name)).ToList();
        return Ok(result);
    }
}

/// <summary>Dùng chung cho controller.</summary>
public sealed record MedicationCatalogItem(
    Guid MedicineId,
    string Name);