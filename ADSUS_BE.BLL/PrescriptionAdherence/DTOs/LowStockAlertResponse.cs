using System;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

public class LowStockAlertResponse
{
    public Guid MedicineId { get; set; }
    public string MedicineName { get; set; } = null!;
    public int CurrentStock { get; set; }     // Tổng QuantityBase còn hạn
    public int Threshold { get; set; }        // Ngưỡng cảnh báo
    public string BaseUnitName { get; set; } = null!;
    public string Severity { get; set; } = null!; // "WARNING" | "CRITICAL"
}
