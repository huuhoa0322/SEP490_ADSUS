using System;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs.Invoice;

public class InvoiceResponse
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public string CaseName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PaymentMethod { get; set; }
}
