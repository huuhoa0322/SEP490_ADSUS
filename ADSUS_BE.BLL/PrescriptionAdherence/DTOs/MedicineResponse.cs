using System;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

public class MedicineResponse
{
    public Guid MedicineId { get; set; }
    public string Name { get; set; } = null!;
    public string? UsageUnit { get; set; }
    public decimal? VolumePerBaseUnit { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public class CreateMedicineRequest
{
    public string Name { get; set; } = null!;
    public string? UsageUnit { get; set; }
    public decimal? VolumePerBaseUnit { get; set; }
    public Guid MedicineUnitId { get; set; }
    public decimal SalePrice { get; set; }
}

public class UpdateMedicineRequest
{
    public string Name { get; set; } = null!;
    public string? UsageUnit { get; set; }
    public decimal? VolumePerBaseUnit { get; set; }
}
