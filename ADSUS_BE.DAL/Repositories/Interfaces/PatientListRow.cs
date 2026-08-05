namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Một dòng của danh sách bệnh nhân (UC-09, #26).
///
/// Không trả entity PatientProfile như trước: truy vấn nay xuất phát từ bảng users, nên có
/// những dòng CHƯA có hồ sơ nền — với chúng thì PatientProfileId là null. Kiểu entity không
/// diễn đạt được trạng thái đó.
/// </summary>
public sealed record PatientListRow(
    Guid? PatientProfileId,
    Guid PatientUserId,
    string FullName,
    string Phone,
    DateOnly? LatestVisitDate,
    string? LatestVisitStatus);
