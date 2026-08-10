using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

public partial class DoctorAnnotation
{
    public Guid AnnotationId { get; set; }

    public Guid CaseId { get; set; }

    public Guid ImageId { get; set; }

    public decimal BboxXmin { get; set; }

    public decimal BboxYmin { get; set; }

    public decimal BboxXmax { get; set; }

    public decimal BboxYmax { get; set; }

    public string Source { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Case Case { get; set; } = null!;

    public virtual UltrasoundImage Image { get; set; } = null!;
}
