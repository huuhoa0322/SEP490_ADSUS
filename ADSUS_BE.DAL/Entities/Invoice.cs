using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

public partial class Invoice
{
    public Guid Id { get; set; }

    public Guid CaseId { get; set; }

    public decimal TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public virtual Case Case { get; set; } = null!;

    public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
}
