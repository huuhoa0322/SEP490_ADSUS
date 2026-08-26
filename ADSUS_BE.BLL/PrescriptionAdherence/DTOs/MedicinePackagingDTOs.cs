using System;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

public class MedicineUnitResponse
{
    public Guid MedicineUnitId { get; set; }
    public string Name { get; set; } = null!;
}

public class MedicinePackagingResponse
{
    public Guid Id { get; set; }
    public Guid MedicineId { get; set; }
    public Guid MedicineUnitId { get; set; }
    public string UnitName { get; set; } = null!;
    public int ConversionFactor { get; set; }
    public bool IsBaseUnit { get; set; }
    public bool IsSellable { get; set; }
    public decimal SalePrice { get; set; }
}

public class CreateMedicinePackagingRequest
{
    public Guid MedicineUnitId { get; set; }
    public int ConversionFactor { get; set; }
    public bool IsBaseUnit { get; set; }
    public bool IsSellable { get; set; }
    public decimal SalePrice { get; set; }
}

public class UpdateMedicinePackagingRequest
{
    public Guid MedicineUnitId { get; set; }
    public int ConversionFactor { get; set; }
    public bool IsBaseUnit { get; set; }
    public bool IsSellable { get; set; }
    public decimal SalePrice { get; set; }
}
