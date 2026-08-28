using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

public partial class InvoiceItem
{
    public Guid Id { get; set; }

    public Guid InvoiceId { get; set; }

    public string Description { get; set; } = null!;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal TotalPrice { get; set; }

    public Guid? ReferenceId { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;
}
