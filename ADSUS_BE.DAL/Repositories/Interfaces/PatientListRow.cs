namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Một dòng của danh sách bệnh nhân (UC-09, #26).
///
/// Không trả entity PatientProfile như trước: truy vấn nay xuất phát từ bảng users, nên có
/// thể có dòng chưa có bản ghi patient_profiles (tài khoản cũ tạo trước 24/09/2026) — với
/// chúng thì PatientProfileId là null. Kiểu entity không diễn đạt được trạng thái đó.
///
/// HasBaselineProfile tách riêng khỏi PatientProfileId: có bản ghi hồ sơ chưa chắc đã lập
/// hồ sơ nền — xem <see cref="PatientProfileBaseline"/>.
/// </summary>
public sealed record PatientListRow(
    Guid? PatientProfileId,
    Guid PatientUserId,
    string FullName,
    string Phone,
    DateOnly? LatestVisitDate,
    string? LatestVisitStatus,
    Guid? LatestCaseId = null,
    bool HasBaselineProfile = false);
