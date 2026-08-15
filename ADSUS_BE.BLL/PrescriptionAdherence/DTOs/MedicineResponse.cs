using System;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

public class MedicineResponse
{
    public Guid MedicineId { get; set; }
    public string Name { get; set; } = null!;
}
