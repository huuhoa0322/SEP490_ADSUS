using System;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

public class AdjustInventoryResponse
{
    public Guid TransactionId { get; set; }
    public int PreviousQuantity { get; set; }
    public int NewQuantity { get; set; }
    public int Delta { get; set; } // Âm = giảm, Dương = tăng
}
