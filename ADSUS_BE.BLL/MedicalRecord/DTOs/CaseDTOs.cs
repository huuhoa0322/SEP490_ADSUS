namespace ADSUS_BE.BLL.MedicalRecord.DTOs;

/// <summary>
/// #21, #22 và nhúng trong #23.
/// imageUrl là URL có hạn do Storage ký; NULL nếu ký thất bại (file không tồn tại).
/// KHÔNG BAO GIỜ trả fileRef — đó là đường dẫn lưu trữ thô.
/// </summary>
public sealed record UltrasoundImageResponse(
    Guid ImageId,
    Guid CaseId,
    string? ImageUrl,
    DateTime UploadedAt,
    string? Note);

/// <summary>Tóm tắt đơn thuốc nhúng trong #23. Chi tiết thuộc Module 7.</summary>
public sealed record PrescriptionSummary(
    Guid PrescriptionId,
    string Status);

/// <summary>
/// #20, #23 — bản đầy đủ cho Bác sĩ/Điều dưỡng (Web SCR-12).
/// </summary>
public sealed record CaseResponse(
    Guid CaseId,
    Guid PatientProfileId,
    Guid DoctorId,
    string DoctorName,
    DateOnly VisitDate,
    string? ClinicalInfo,
    string Status,
    string? FinalDiagnosis,
    string? DoctorConclusion,
    PatientProfileResponse? PatientProfile,
    IReadOnlyList<UltrasoundImageResponse> UltrasoundImages,
    // AiResults removed
    PrescriptionSummary? Prescription,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// #23 khi người gọi là Bệnh nhân (Mobile SCR-14).
///
/// Là một KIỂU RIÊNG chứ không phải CaseResponse với vài field để null: quy tắc dữ liệu nhạy
/// cảm nói field mà người gọi không có quyền thì không được KHAI BÁO, mà class C# thì không
/// ẩn field được — null vẫn serialize ra. Tách kiểu là cách duy nhất để trình biên dịch bảo
/// đảm clinicalInfo / ultrasoundImages / aiResults không bao giờ tới tay bệnh nhân (GB-05).
/// </summary>
public sealed record PatientCaseResponse(
    Guid CaseId,
    Guid DoctorId,
    string DoctorName,
    DateOnly VisitDate,
    string Status,
    string? FinalDiagnosis,
    string? DoctorConclusion,
    PrescriptionSummary? Prescription);

/// <summary>#24, #25 — một dòng trong danh sách lần khám.</summary>
public sealed record CaseSummaryResponse(
    Guid CaseId,
    DateOnly VisitDate,
    string Status,
    Guid DoctorId);

/// <summary>#20 — tạo lần khám mới kèm ít nhất một ảnh siêu âm (UC-07).</summary>
public sealed record CreateCaseRequest(
    Guid PatientProfileId,
    Guid ResponsibleDoctorId,
    string? ClinicalInfo,
    IReadOnlyList<UploadedFile> Images);

/// <summary>#21 — bổ sung ảnh vào một ca chưa được chốt (UC-07).</summary>
public sealed record AddUltrasoundImagesRequest(
    IReadOnlyList<UploadedFile> Images,
    string? Note);
