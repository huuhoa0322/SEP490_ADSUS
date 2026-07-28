using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Đơn thuốc kê sau lượt khám (UC-18). Header — chi tiết thuốc nằm ở prescription_items.
/// </summary>
public partial class Prescription
{
    public Guid PrescriptionId { get; set; }

    public Guid CaseId { get; set; }

    public Guid DoctorId { get; set; }

    public DateOnly PrescribedDate { get; set; }

    public string? GeneralNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Case Case { get; set; } = null!;

    public virtual User Doctor { get; set; } = null!;

    public virtual ICollection<PrescriptionItem> PrescriptionItems { get; set; } = new List<PrescriptionItem>();
}
