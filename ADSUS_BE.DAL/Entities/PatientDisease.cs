using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

public partial class PatientDisease
{
    public Guid Id { get; set; }

    public Guid PatientProfileId { get; set; }

    public Guid DiseaseId { get; set; }

    public string? Note { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual MedicalDisease Disease { get; set; } = null!;

    public virtual PatientProfile PatientProfile { get; set; } = null!;
}
