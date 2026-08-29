using System;

namespace ADSUS_BE.DAL.Entities;

public partial class Invoice
{
    [System.ComponentModel.DataAnnotations.Schema.Column("status")]
    public InvoiceStatus Status { get; set; } = InvoiceStatus.PENDING;
    
    [System.ComponentModel.DataAnnotations.Schema.Column("payment_method")]
    public PaymentMethod? PaymentMethod { get; set; }
}
