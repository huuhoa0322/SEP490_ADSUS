using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

public partial class MedicalDisease
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public bool RequiresNote { get; set; }

    public bool IsOther { get; set; }

    public virtual ICollection<PatientDisease> PatientDiseases { get; set; } = new List<PatientDisease>();
}
