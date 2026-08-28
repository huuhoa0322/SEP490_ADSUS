using System;
using System.Collections.Generic;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs.Invoice;

public class InvoiceDetailResponse : InvoiceResponse
{
    public List<InvoiceItemResponse> Items { get; set; } = new();
}

public class InvoiceItemResponse
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}
