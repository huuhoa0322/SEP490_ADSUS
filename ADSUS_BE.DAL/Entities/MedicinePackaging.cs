using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

public partial class MedicinePackaging
{
    public Guid Id { get; set; }

    public Guid MedicineId { get; set; }

    public Guid MedicineUnitId { get; set; }

    public int ConversionFactor { get; set; }

    public bool IsBaseUnit { get; set; }

    public bool IsSellable { get; set; }

    public decimal SalePrice { get; set; }

    public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();

    public virtual Medicine Medicine { get; set; } = null!;

    public virtual MedicineUnit MedicineUnit { get; set; } = null!;
}
