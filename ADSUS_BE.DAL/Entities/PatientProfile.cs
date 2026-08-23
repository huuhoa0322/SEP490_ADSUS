using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Hồ sơ y tế nền của bệnh nhân (1–1 với users). Tách khỏi users để thực thi quy tắc lõi: Admin quản tài khoản nhưng KHÔNG truy cập dữ liệu y tế (§3.2) — ngoại lệ duy nhất là date_of_birth, đã chuyển lên users vì dùng chung cho cả 3 vai trò. user_id phải có role = PATIENT, created_by phải có role = DOCTOR — enforce ở tầng ứng dụng (FK không kiểm tra được role).
/// </summary>
public partial class PatientProfile
{
    public Guid PatientProfileId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// Bác sĩ lập hồ sơ (UC-06). Bệnh nhân không tự đăng ký.
    /// </summary>
    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<Case> Cases { get; set; } = new List<Case>();

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<HealthLog> HealthLogs { get; set; } = new List<HealthLog>();

    public virtual ICollection<PatientAllergy> PatientAllergies { get; set; } = new List<PatientAllergy>();

    public virtual ICollection<PatientDisease> PatientDiseases { get; set; } = new List<PatientDisease>();

    public virtual PatientReminderPreference? PatientReminderPreference { get; set; }

    public virtual ICollection<ServiceFeedback> ServiceFeedbacks { get; set; } = new List<ServiceFeedback>();

    public virtual User User { get; set; } = null!;
}
