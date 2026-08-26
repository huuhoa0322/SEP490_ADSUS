using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

public partial class MedicineBatch
{
    public Guid Id { get; set; }

    public Guid MedicineId { get; set; }

    public string LotNumber { get; set; } = null!;

    public DateOnly ExpiryDate { get; set; }

    public int QuantityBase { get; set; }

    public Guid? SupplierId { get; set; }

    public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();

    public virtual Medicine Medicine { get; set; } = null!;

    public virtual Supplier? Supplier { get; set; }
}
