using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

public partial class Symptom
{
    public Guid SymptomId { get; set; }

    public Guid CategoryId { get; set; }

    public string Name { get; set; } = null!;

    public bool IsOther { get; set; }

    public virtual ICollection<CaseSymptom> CaseSymptoms { get; set; } = new List<CaseSymptom>();

    public virtual SymptomCategory Category { get; set; } = null!;
}
