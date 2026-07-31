namespace ADSUS_BE.BLL.UserRoleManagement.Interfaces;

/// <summary>
/// Cổng gửi email của hệ thống (API-04 trong PRD).
///
/// Có hai bản hiện thực:
///   <c>SmtpEmailService</c> — bản thật, dùng khi đã khai EmailSettings.
///   <c>DevConsoleEmailService</c> — in mật khẩu tạm ra console, CHỈ ở Development và chỉ
///   khi chưa khai SMTP, để cả nhóm không bị chặn vì thiếu tài khoản gửi mail.
/// Program.cs chọn bản nào; ngoài Development mà chưa khai SMTP thì dừng ngay lúc khởi động.
///
/// Hai use case đang cần: UC-04 (Admin tạo tài khoản) và UC-03 (người dùng tự quên mật khẩu).
/// Cả hai đều gửi đúng một loại nội dung nên chỉ cần một phương thức.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Gửi mật khẩu tạm cho chủ tài khoản.
    /// </summary>
    /// <param name="toEmail">Email đã đăng ký của tài khoản.</param>
    /// <param name="fullName">Họ tên, để xưng hô trong thư.</param>
    /// <param name="temporaryPassword">
    /// Mật khẩu thô. Đây là nơi DUY NHẤT trong toàn hệ thống nhìn thấy nó ở dạng đọc được —
    /// không được ghi ra log, không được trả về cho client, không được lưu lại.
    /// </param>
    /// <returns>
    /// true nếu gửi được. KHÔNG ném ngoại lệ khi gửi thất bại — bên gọi phải tự quyết định
    /// dựa trên giá trị này, và mỗi chỗ quyết định một khác:
    ///   UC-04 tạo tài khoản — vẫn giữ tài khoản (số điện thoại đã bị chiếm rồi), chỉ báo
    ///   cho Admin biết là còn phải cấp lại mật khẩu.
    ///   UC-03 cấp lại mật khẩu — KHÔNG lưu mật khẩu mới, giữ nguyên mật khẩu cũ, nếu không
    ///   thì chủ tài khoản bị nhốt ở ngoài.
    ///
    /// Vì vậy TUYỆT ĐỐI không được bỏ qua giá trị trả về.
    /// </returns>
    Task<bool> SendTemporaryPasswordAsync(
        string toEmail,
        string fullName,
        string temporaryPassword,
        CancellationToken cancellationToken = default);
}
