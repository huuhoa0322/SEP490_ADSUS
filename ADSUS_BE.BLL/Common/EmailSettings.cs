namespace ADSUS_BE.BLL.Common;

/// <summary>
/// Cấu hình máy chủ gửi mail (API-04 trong PRD), đọc từ User Secrets.
///
/// TUYỆT ĐỐI không đặt trong appsettings.json — file đó được commit, mà ở đây có mật khẩu.
/// Mỗi người tự khai trong User Secrets của máy mình, giống JwtSettings.
///
/// Ví dụ với Gmail (chuột phải project ADSUS_BE &gt; Manage User Secrets):
/// <code>
/// "EmailSettings": {
///   "SmtpHost": "smtp.gmail.com",
///   "SmtpPort": 587,
///   "Username": "adsus.noreply@gmail.com",
///   "Password": "&lt;App Password 16 ky tu&gt;",
///   "FromAddress": "adsus.noreply@gmail.com",
///   "FromName": "ADSUS"
/// }
/// </code>
///
/// Lưu ý về Gmail: phải bật xác minh 2 bước rồi tạo "App Password" riêng. Mật khẩu đăng
/// nhập Gmail thông thường bị Google từ chối thẳng, báo lỗi 535 — không phải lỗi code.
/// </summary>
public class EmailSettings
{
    public const string SectionName = "EmailSettings";

    /// <summary>
    /// Địa chỉ máy chủ SMTP. Bỏ trống nghĩa là chưa cấu hình gửi mail —
    /// xem cách Program.cs xử lý trường hợp đó.
    /// </summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>
    /// Cổng SMTP. 587 là cổng STARTTLS chuẩn, dùng được cho cả Gmail lẫn Outlook.
    /// Cổng 25 hầu như luôn bị nhà mạng chặn, đừng dùng.
    /// </summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>Tài khoản đăng nhập SMTP. Với Gmail chính là địa chỉ email.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Mật khẩu SMTP (Gmail: App Password). Chỉ nằm trong User Secrets.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Địa chỉ hiện ở ô "Từ". Bỏ trống thì dùng luôn <see cref="Username"/>.
    /// Gmail bắt buộc địa chỉ này phải trùng tài khoản đăng nhập, đặt khác là bị đổi lại.
    /// </summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Tên hiển thị của người gửi.</summary>
    public string FromName { get; set; } = "ADSUS";

    /// <summary>
    /// Bật mã hoá đường truyền. Luôn để true với cổng 587 — tắt đi là mật khẩu tạm
    /// đi qua mạng ở dạng đọc được.
    /// </summary>
    public bool UseStartTls { get; set; } = true;

    /// <summary>Thời gian chờ tối đa khi gửi, tính bằng giây.</summary>
    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>Đã khai đủ để gửi được hay chưa.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(SmtpHost);
}
