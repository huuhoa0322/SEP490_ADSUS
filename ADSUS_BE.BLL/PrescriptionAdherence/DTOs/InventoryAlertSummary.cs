using System.Collections.Generic;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

public class InventoryAlertSummary
{
    public int LowStockCount { get; set; }
    public int ExpiringSoonCount { get; set; }  // ≤60 ngày
    public int ExpiredCount { get; set; }       // đã hết hạn
    public List<LowStockAlertResponse> LowStockAlerts { get; set; } = new();
    public List<ExpiryAlertResponse> ExpiryAlerts { get; set; } = new();
}
