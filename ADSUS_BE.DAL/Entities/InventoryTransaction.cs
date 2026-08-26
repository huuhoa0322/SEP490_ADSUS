using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

public partial class InventoryTransaction
{
    public Guid Id { get; set; }

    public Guid BatchId { get; set; }

    public Guid MedicinePackagingId { get; set; }

    public int QuantityInUnit { get; set; }

    public int QuantityBase { get; set; }

    public DateTime TxnDate { get; set; }

    public Guid? ReferencePrescriptionItemId { get; set; }

    public virtual MedicineBatch Batch { get; set; } = null!;

    public virtual MedicinePackaging MedicinePackaging { get; set; } = null!;

    public virtual PrescriptionItem? ReferencePrescriptionItem { get; set; }
}
