using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

public partial class PatientAllergy
{
    public Guid Id { get; set; }

    public Guid PatientProfileId { get; set; }

    public Guid AllergyTypeId { get; set; }

    public string? Note { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual MedicalAllergyType AllergyType { get; set; } = null!;

    public virtual PatientProfile PatientProfile { get; set; } = null!;
}
