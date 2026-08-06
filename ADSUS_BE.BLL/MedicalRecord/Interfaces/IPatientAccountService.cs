using ADSUS_BE.BLL.MedicalRecord.DTOs;

namespace ADSUS_BE.BLL.MedicalRecord.Interfaces;

/// <summary>
/// UC-06 AF-01/AF-02/AF-03 (quyết định ghi đè 04/08/2026) — Điều dưỡng quản lý tài khoản
/// Bệnh nhân ngay trong luồng tiếp nhận.
///
/// PHẠM VI HẸP, đọc kỹ BR-03 trước khi mở rộng: chỉ tài khoản role PATIENT, chỉ 3 hành động
/// dưới đây. Tạo tài khoản Doctor/Nurse/Admin, khoá/mở khoá/vô hiệu hoá, gán role — tất cả
/// vẫn là UC-04 (Admin), không đụng tới ở đây.
///
/// Chỉ Điều dưỡng, KHÔNG phải Bác sĩ. Đây là ngoại lệ đầu tiên trong bộ quyền vốn giống hệt
/// nhau giữa hai vai trò.
/// </summary>
public interface IPatientAccountService
{
    /// <summary>AF-01 — tạo tài khoản Bệnh nhân mới, sinh mật khẩu tạm, trả về plaintext đúng
    /// một lần để Điều dưỡng đọc cho bệnh nhân (quyết định ghi đè 06/08/2026 — không còn gửi
    /// email).</summary>
    Task<PatientAccountCreatedResponse> CreateAsync(
        CreatePatientAccountRequest request, Guid actingNurseId, CancellationToken ct = default);

    /// <summary>AF-02 — sửa 4 trường liên hệ. Không đụng role/status.</summary>
    Task<PatientAccountResponse> UpdateContactAsync(
        Guid userId, UpdatePatientAccountRequest request, Guid actingNurseId, CancellationToken ct = default);

    /// <summary>
    /// AF-03 — sinh mật khẩu tạm mới. Có email thì gửi âm thầm (trả về null); không có email
    /// thì trả plaintext MỘT LẦN (quyết định ghi đè 06/08/2026, thay một phần BR-05).
    /// </summary>
    Task<string?> ResetPasswordAsync(Guid userId, Guid actingNurseId, CancellationToken ct = default);
}
