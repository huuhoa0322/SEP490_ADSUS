using System;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs.Invoice;

public class InvoiceFilter
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
}
