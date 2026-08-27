using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Một lượt khám của một bệnh nhân — mốc neo cho ảnh siêu âm, kết quả AI, đơn thuốc. Theo dõi tiến triển (FT-22) = so sánh dữ liệu qua nhiều cases theo visit_date. Vòng đời CREATED → ANALYZED → CONFIRMED một chiều (GBR) — enforce ở tầng ứng dụng.
/// </summary>
public partial class Case
{
    public Guid CaseId { get; set; }

    public Guid PatientProfileId { get; set; }

    public Guid DoctorId { get; set; }

    public DateOnly VisitDate { get; set; }

    /// <summary>
    /// Thông tin lâm sàng bác sĩ nhập khi tạo ca (FT-14) — đầu vào phụ trợ cho AI.
    /// </summary>
    public string? ClinicalInfo { get; set; }

    /// <summary>
    /// Kết luận chẩn đoán cuối của bác sĩ SAU khi duyệt kết quả AI — mỗi ca đúng 1 kết luận (attribute, không tách entity).
    /// </summary>
    public string? FinalDiagnosis { get; set; }

    public string? DoctorConclusion { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<AiPrediction> AiPredictions { get; set; } = new List<AiPrediction>();

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<CaseSymptom> CaseSymptoms { get; set; } = new List<CaseSymptom>();

    public virtual User Doctor { get; set; } = null!;

    public virtual ICollection<DoctorAnnotation> DoctorAnnotations { get; set; } = new List<DoctorAnnotation>();

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual PatientProfile PatientProfile { get; set; } = null!;

    public virtual ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();

    public virtual ServiceFeedback? ServiceFeedback { get; set; }

    public virtual ICollection<UltrasoundImage> UltrasoundImages { get; set; } = new List<UltrasoundImage>();
}
