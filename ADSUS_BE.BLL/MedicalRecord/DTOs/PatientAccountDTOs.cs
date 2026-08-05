namespace ADSUS_BE.BLL.MedicalRecord.DTOs;

/// <summary>
/// UC-06 AF-01 (quyết định ghi đè 04/08/2026) — Điều dưỡng tạo tài khoản Bệnh nhân ngay tại
/// luồng tiếp nhận, thay vì phải nhờ Admin.
///
/// KHÔNG có Role: luôn cố định PATIENT. Nhận role từ request thì Điều dưỡng tự tạo được tài
/// khoản Bác sĩ cho mình.
///
/// KHÔNG có mật khẩu: hệ thống sinh mật khẩu tạm rồi gửi email, y hệt UC-04 BR-03. Điều
/// dưỡng không bao giờ được tự đặt (BR-05).
/// </summary>
public sealed record CreatePatientAccountRequest(
    string PhoneNumber,
    string FullName,
    DateOnly? DateOfBirth,
    string? Email);

/// <summary>
/// UC-06 AF-02 — Điều dưỡng sửa lỗi nhập liệu trên tài khoản Bệnh nhân.
///
/// Đúng 4 trường đã nhập lúc tạo (BR-04). KHÔNG có role, KHÔNG có status — đổi hai thứ đó
/// vẫn hoàn toàn là việc của Admin (UC-04).
/// </summary>
public sealed record UpdatePatientAccountRequest(
    string FullName,
    string PhoneNumber,
    DateOnly? DateOfBirth,
    string? Email);

/// <summary>
/// Trả về sau khi tạo/sửa tài khoản Bệnh nhân.
///
/// CÓ DateOfBirth — khác hẳn UserAccountResponse của Module 2, nơi trường này LUÔN null với
/// vai trò PATIENT (UC-04 BR-01, Admin không được thấy ngày sinh bệnh nhân). Ở đây người gọi
/// là Điều dưỡng, và ngày sinh là dữ liệu lâm sàng họ cần thấy (UC-06 bước 2).
///
/// KHÔNG có mật khẩu tạm dưới bất kỳ hình thức nào (BR-05, PRD §6.2).
/// </summary>
public sealed record PatientAccountResponse(
    Guid UserId,
    string FullName,
    string PhoneNumber,
    DateOnly? DateOfBirth,
    string? Email);
