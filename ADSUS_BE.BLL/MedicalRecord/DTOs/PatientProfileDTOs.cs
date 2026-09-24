using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.MedicalRecord.DTOs;

/// <summary>
/// #17 — tạo hồ sơ nền (UC-06).
/// createdBy KHÔNG nằm ở đây: nó lấy từ token của người đang thao tác, nhận từ body thì ai
/// cũng ghi tên người khác vào được.
/// Gender sẽ được set vào User entity (2026-01 - đã chuyển từ PatientProfile).
/// </summary>
public sealed record CreatePatientProfileRequest(
    Guid PatientUserId,
    string? Gender, // Set vào User.Gender
    IReadOnlyList<PatientDiseaseInput>? Diseases,
    IReadOnlyList<PatientAllergyInput>? Allergies);

/// <summary>
/// #18 — thay toàn bộ hồ sơ nền (UC-06). patientUserId không sửa được: quan hệ 1–1 chốt lúc tạo.
/// Gender sẽ được set vào User entity (2026-01 - đã chuyển từ PatientProfile).
/// </summary>
public sealed record UpdatePatientProfileRequest(
    string? Gender, // Set vào User.Gender
    IReadOnlyList<PatientDiseaseInput>? Diseases,
    IReadOnlyList<PatientAllergyInput>? Allergies);

/// <summary>
/// #17, #18, #19 và nhúng trong #23.
/// fullName/phone/dateOfBirth là dữ liệu chỉ đọc lấy từ bảng users (UC-06 bước 2).
/// Gender lấy từ users (2026-01).
/// </summary>
public sealed record PatientProfileResponse(
    Guid PatientProfileId,
    Guid PatientUserId,
    string FullName,
    string Phone,
    DateOnly? DateOfBirth,
    string? Gender, // Lấy từ User.Gender (nullable vì User.Gender cũng nullable)
    IReadOnlyList<PatientDiseaseResponse> Diseases,
    IReadOnlyList<PatientAllergyResponse> Allergies,
    Guid CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// #26 — một dòng trong danh sách bệnh nhân của bác sĩ (UC-09).
/// KHÔNG có email, trạng thái tài khoản, mustChangePassword — đó là dữ liệu quản trị tài
/// khoản của Module 2, không thuộc màn hình lâm sàng này.
///
/// HasBaselineProfile false nghĩa là chưa được lập hồ sơ nền (UC-06) — giao diện hiện nhãn
/// "Chưa lập hồ sơ nền" và nút "Tạo hồ sơ nền". PatientProfileId vẫn có thể khác null lúc đó:
/// mọi tài khoản PATIENT đều có bản ghi hồ sơ tạo sẵn để đặt lịch được ngay (UC-13). Chỉ tài
/// khoản cũ tạo trước 24/09/2026 mới có thể còn PatientProfileId null.
/// </summary>
public sealed record PatientSummaryResponse(
    Guid? PatientProfileId,
    Guid PatientUserId,
    string FullName,
    string Phone,
    DateOnly? LatestVisitDate,
    string? LatestVisitStatus,
    Guid? LatestCaseId = null,
    bool HasBaselineProfile = false);

public sealed record PatientDiseaseInput(Guid DiseaseId, string? Note);
public sealed record PatientAllergyInput(Guid AllergyTypeId, string? Note);

public sealed record PatientDiseaseResponse(Guid DiseaseId, string DiseaseName, bool IsOther, string? Note);
public sealed record PatientAllergyResponse(Guid AllergyTypeId, string AllergyName, bool IsOther, string? Note);

