using System;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

public class ExpiryAlertResponse
{
    public Guid BatchId { get; set; }
    public Guid MedicineId { get; set; }
    public string MedicineName { get; set; } = null!;
    public string LotNumber { get; set; } = null!;
    public DateTime ExpiryDate { get; set; }
    public int DaysUntilExpiry { get; set; }
    public int QuantityBase { get; set; }
    public string BaseUnitName { get; set; } = null!;
    public string Severity { get; set; } = null!; // "WARNING" | "CRITICAL" | "EXPIRED"
}
