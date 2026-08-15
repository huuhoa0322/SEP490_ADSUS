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

/// <summary>1 loại thuốc trong đơn — nhúng trong PrescriptionSummary.</summary>
public sealed record PrescriptionItemSummary(
    string MedicineName,
    string Dosage,
    short DurationDays,
    DateOnly StartDate,
    string? Instructions);

/// <summary>
/// Đơn thuốc nhúng trong #23. Đính chính 15/08/2026: trước đây chỉ có PrescriptionId+Status
/// ("chi tiết thuộc Module 7") — Module 7 xác nhận CHƯA có màn nào xem chi tiết đơn thuốc, nên
/// giờ hiện đầy đủ thẳng ở đây (dùng chung cho cả CaseResponse lẫn PatientCaseResponse).
/// </summary>
public sealed record PrescriptionSummary(
    Guid PrescriptionId,
    string Status,
    DateOnly PrescribedDate,
    string? GeneralNote,
    IReadOnlyList<PrescriptionItemSummary> Items);

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
/// đảm clinicalInfo / patientProfile / aiResults không bao giờ tới tay bệnh nhân (GB-05).
///
/// Đính chính 15/08/2026: trước đây comment này còn liệt cả ultrasoundImages vào danh sách bị
/// cấm — SAI theo quyết định UCS 01/08/2026 (xem CasesController.cs's ExportReport doc comment
/// + design spec 2026-08-15): Patient CÓ xem được ảnh siêu âm gốc một khi ca đã Confirmed/End,
/// giống hệt nội dung PDF export (trừ chức năng xuất file). Chỉ dữ liệu nội bộ thuần Staff
/// (ClinicalInfo, PatientProfile, AiResults, các mốc CreatedAt/UpdatedAt) mới còn bị cấm.
/// </summary>
public sealed record PatientCaseResponse(
    Guid CaseId,
    Guid DoctorId,
    string DoctorName,
    DateOnly VisitDate,
    string Status,
    string? FinalDiagnosis,
    string? DoctorConclusion,
    PrescriptionSummary? Prescription,
    IReadOnlyList<UltrasoundImageResponse> UltrasoundImages);

/// <summary>#25 — một dòng trong danh sách lần khám CỦA CHÍNH bệnh nhân (Mobile).</summary>
public sealed record CaseSummaryResponse(
    Guid CaseId,
    DateOnly VisitDate,
    string Status,
    Guid DoctorId);

/// <summary>
/// #24 — một dòng trong danh sách lần khám cho Bác sĩ/Điều dưỡng (Web SCR-12).
///
/// Tách riêng khỏi CaseSummaryResponse (thêm 06/08/2026) chỉ để có thêm CreatedAt — VisitDate
/// là DateOnly, không có giờ. KHÔNG dùng chung với #25: PatientCaseResponse (chi tiết 1 ca cho
/// bệnh nhân) đã cố tình bỏ mọi mốc thời gian, nên danh sách của bệnh nhân cũng không nên có,
/// tránh lệch giữa hai màn cùng vai trò.
/// </summary>
public sealed record StaffCaseSummaryResponse(
    Guid CaseId,
    DateOnly VisitDate,
    string Status,
    Guid DoctorId,
    DateTime CreatedAt);

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

/// <summary>
/// Thêm 07/08/2026, sửa lại cùng ngày (tách Lưu/Kết thúc) — Bác sĩ phụ trách nhập/sửa kết
/// luận cho ca khám ngay tại màn chi tiết ca (Module 04), không đợi màn duyệt kết quả AI
/// riêng (UC-19, đang được xây song song bởi một luồng công việc khác).
///
/// Dùng chung cho HAI hành động khác nhau, xem ICaseService:
///   SaveConclusionAsync — chỉ lưu nội dung, KHÔNG đổi trạng thái, sửa lại được nhiều lần.
///   ConfirmAsync        — lưu VÀ khoá ca (CONFIRMED), không có đường lùi (GB-01/P2).
/// </summary>
public sealed record CaseConclusionRequest(
    string FinalDiagnosis,
    string DoctorConclusion);
