using System;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

public class MedicineResponse
{
    public Guid MedicineId { get; set; }
    public string Name { get; set; } = null!;
    public string? UsageUnit { get; set; }         // Đơn vị dùng khi kê đơn
    public string? BaseUnitName { get; set; }      // Tên đơn vị cơ bản kho (IsBaseUnit=true)
    public decimal? VolumePerBaseUnit { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public int LowStockThreshold { get; set; }
    public int TotalInventoryBase { get; set; }
}

public class CreateMedicineRequest
{
    public string Name { get; set; } = null!;
    public string? UsageUnit { get; set; }
    public decimal? VolumePerBaseUnit { get; set; }
    public Guid MedicineUnitId { get; set; }
    public decimal SalePrice { get; set; }
    public int LowStockThreshold { get; set; }
}

public class UpdateMedicineRequest
{
    public string Name { get; set; } = null!;
    public string? UsageUnit { get; set; }
    public decimal? VolumePerBaseUnit { get; set; }
    public int LowStockThreshold { get; set; }
}
