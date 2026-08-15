using System;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

public class MedicineResponse
{
    public Guid MedicineId { get; set; }
    public string Name { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public class CreateMedicineRequest
{
    public string Name { get; set; } = null!;
}

public class UpdateMedicineRequest
{
    public string Name { get; set; } = null!;
}
