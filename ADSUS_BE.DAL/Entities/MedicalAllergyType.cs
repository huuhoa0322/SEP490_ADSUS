using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

public partial class MedicalAllergyType
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public bool IsOther { get; set; }

    public virtual ICollection<PatientAllergy> PatientAllergies { get; set; } = new List<PatientAllergy>();
}
