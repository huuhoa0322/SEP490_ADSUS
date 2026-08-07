namespace ADSUS_BE.BLL.MedicalRecord.DTOs;

/// <summary>
/// UC-06 AF-01 (quyết định ghi đè 04/08/2026) — Điều dưỡng tạo tài khoản Bệnh nhân ngay tại
/// luồng tiếp nhận, thay vì phải nhờ Admin.
///
/// KHÔNG có Role: luôn cố định PATIENT. Nhận role từ request thì Điều dưỡng tự tạo được tài
/// khoản Bác sĩ cho mình.
///
/// KHÔNG có mật khẩu: hệ thống sinh mật khẩu tạm (Điều dưỡng không bao giờ tự ĐẶT — vẫn đúng
/// BR-05 phần này). Khác trước 06/08/2026: mật khẩu đó giờ trả về plaintext MỘT LẦN trong
/// response của endpoint tạo tài khoản (PatientAccountCreatedResponse) để hiển thị ngay trên
/// màn hình, KHÔNG còn gửi qua email.
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

/// <summary>
/// Trả về SAU KHI TẠO tài khoản Bệnh nhân — DUY NHẤT nơi mật khẩu tạm xuất hiện dưới dạng
/// plaintext, và chỉ đúng một lần ngay tại thời điểm tạo (quyết định ghi đè 06/08/2026, thay
/// một phần BR-05 gốc). Điều dưỡng đọc trực tiếp cho bệnh nhân nghe/ghi lại tại chỗ — KHÔNG
/// còn gửi qua email. Không endpoint nào khác của Module 04 trả plaintext mật khẩu; DB chỉ
/// lưu bản băm (PasswordHash).
/// </summary>
public sealed record PatientAccountCreatedResponse(
    Guid UserId,
    string FullName,
    string PhoneNumber,
    DateOnly? DateOfBirth,
    string? Email,
    string TemporaryPassword);
