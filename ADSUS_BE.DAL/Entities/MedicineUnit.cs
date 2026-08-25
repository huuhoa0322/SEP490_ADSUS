using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

public partial class MedicineUnit
{
    public Guid MedicineUnitId { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<MedicinePackaging> MedicinePackagings { get; set; } = new List<MedicinePackaging>();
}
