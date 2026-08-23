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
    IReadOnlyList<PatientDiseaseInput> Diseases,
    IReadOnlyList<PatientAllergyInput> Allergies);

/// <summary>
/// #18 — thay toàn bộ hồ sơ nền (UC-06). patientUserId không sửa được: quan hệ 1–1 chốt lúc tạo.
/// </summary>
public sealed record UpdatePatientProfileRequest(
    string Gender,
    IReadOnlyList<PatientDiseaseInput> Diseases,
    IReadOnlyList<PatientAllergyInput> Allergies);

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
/// PatientProfileId NULL nghĩa là tài khoản đã tồn tại nhưng chưa được lập hồ sơ nền — giao
/// diện đổi nút hành động thành "Tạo hồ sơ nền" thay vì "Xem hồ sơ".
/// </summary>
public sealed record PatientSummaryResponse(
    Guid? PatientProfileId,
    Guid PatientUserId,
    string FullName,
    string Phone,
    DateOnly? LatestVisitDate,
    string? LatestVisitStatus);

public sealed record PatientDiseaseInput(Guid DiseaseId, string? Note);
public sealed record PatientAllergyInput(Guid AllergyTypeId, string? Note);

public sealed record PatientDiseaseResponse(Guid DiseaseId, string DiseaseName, bool IsOther, string? Note);
public sealed record PatientAllergyResponse(Guid AllergyTypeId, string AllergyName, bool IsOther, string? Note);

