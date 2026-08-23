using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

public partial class SymptomCategory
{
    public Guid CategoryId { get; set; }

    public string Name { get; set; } = null!;

    public bool IsOther { get; set; }

    public virtual ICollection<CaseSymptom> CaseSymptoms { get; set; } = new List<CaseSymptom>();

    public virtual ICollection<Symptom> Symptoms { get; set; } = new List<Symptom>();
}
