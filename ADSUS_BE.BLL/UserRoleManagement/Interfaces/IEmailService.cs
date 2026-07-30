namespace ADSUS_BE.BLL.UserRoleManagement.Interfaces;

/// <summary>
/// Cổng gửi email của hệ thống (API-04 trong PRD).
///
/// ====================================================================================
/// PHẦN NÀY DO NGƯỜI KHÁC LÀM — đây chỉ là chỗ chừa sẵn để ráp vào.
/// Repo hiện chưa có thư viện gửi mail nào (không MailKit, không SendGrid).
/// Khi làm xong, chỉ cần viết một lớp hiện thực giao diện này rồi đăng ký trong
/// Program.cs thay cho <c>DevConsoleEmailService</c>. Không phải sửa gì ở tầng nghiệp vụ.
/// ====================================================================================
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
    /// true nếu gửi được. KHÔNG ném ngoại lệ khi gửi thất bại: tài khoản đã tạo xong rồi,
    /// làm hỏng cả thao tác chỉ vì máy chủ mail trục trặc là không đáng.
    /// </returns>
    Task<bool> SendTemporaryPasswordAsync(
        string toEmail,
        string fullName,
        string temporaryPassword,
        CancellationToken cancellationToken = default);
}
