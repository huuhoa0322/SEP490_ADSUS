using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.MedicalRecord.DTOs;

/// <summary>
/// #17 — tạo hồ sơ nền (UC-06).
/// createdBy KHÔNG nằm ở đây: nó lấy từ token của người đang thao tác, nhận từ body thì ai
/// cũng ghi tên người khác vào được.
/// </summary>
public sealed record CreatePatientProfileRequest(
    Guid PatientUserId,
    string? Gender,
    string? MedicalHistory,
    string? Allergies);

/// <summary>
/// #18 — thay toàn bộ hồ sơ nền (UC-06). patientUserId không sửa được: quan hệ 1–1 chốt lúc tạo.
/// </summary>
public sealed record UpdatePatientProfileRequest(
    string Gender,
    string? MedicalHistory,
    string? Allergies);

/// <summary>
/// #17, #18, #19 và nhúng trong #23.
/// fullName/phone/dateOfBirth là dữ liệu chỉ đọc lấy từ bảng users (UC-06 bước 2).
/// </summary>
public sealed record PatientProfileResponse(
    Guid PatientProfileId,
    Guid PatientUserId,
    string FullName,
    string Phone,
    DateOnly? DateOfBirth,
    string Gender,
    string? MedicalHistory,
    string? Allergies,
    Guid CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// #26 — một dòng trong danh sách bệnh nhân của bác sĩ (UC-09).
/// KHÔNG có email, trạng thái tài khoản, mustChangePassword — đó là dữ liệu quản trị tài
/// khoản của Module 2, không thuộc màn hình lâm sàng này.
/// </summary>
public sealed record PatientSummaryResponse(
    Guid PatientProfileId,
    Guid PatientUserId,
    string FullName,
    string Phone,
    DateOnly? LatestVisitDate,
    string? LatestVisitStatus);
