using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

public partial class AiPrediction
{
    public Guid PredictionId { get; set; }

    public Guid CaseId { get; set; }

    public Guid ImageId { get; set; }

    public Guid ModelVersionId { get; set; }

    public decimal BboxXmin { get; set; }

    public decimal BboxYmin { get; set; }

    public decimal BboxXmax { get; set; }

    public decimal BboxYmax { get; set; }

    public decimal Confidence { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Case Case { get; set; } = null!;

    public virtual UltrasoundImage Image { get; set; } = null!;

    public virtual AiModelVersion ModelVersion { get; set; } = null!;
}
