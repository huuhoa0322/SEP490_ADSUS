using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Danh bạ người thân của user
/// </summary>
public partial class PatientRelationship
{
    public Guid RelationshipId { get; set; }

    public Guid UserId { get; set; }

    public Guid PatientProfileId { get; set; }

    /// <summary>
    /// Nhãn tùy chỉnh: Vợ, Mẹ, Con gái...
    /// </summary>
    public string? RelationshipName { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual PatientProfile PatientProfile { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
