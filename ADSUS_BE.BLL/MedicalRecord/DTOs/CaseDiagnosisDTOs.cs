using System;
using System.Collections.Generic;


namespace ADSUS_BE.BLL.MedicalRecord.DTOs;

public class BBoxDto
{
    public decimal Xmin { get; set; }
    public decimal Ymin { get; set; }
    public decimal Xmax { get; set; }
    public decimal Ymax { get; set; }
    public decimal Confidence { get; set; }
}

public class ConfirmAnalysisRequest
{
    public Stream OriginalImageStream { get; set; } = null!;
    public string OriginalImageContentType { get; set; } = null!;
    public string OriginalImageFileName { get; set; } = null!;

    public Stream BurntImageStream { get; set; } = null!;
    public string BurntImageContentType { get; set; } = null!;
    public string BurntImageFileName { get; set; } = null!;
    public string AiPredictionsJson { get; set; } = "[]";
    public string DoctorAnnotationsJson { get; set; } = "[]";
    public Guid ModelVersionId { get; set; }
    public string? Note { get; set; }
}
