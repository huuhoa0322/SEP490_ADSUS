namespace ADSUS_BE.BLL.Common;

/// <summary>
/// Cấu hình gửi mail qua Resend (REST API qua HTTPS), đọc từ User Secrets — cùng cách chia
/// với <see cref="EmailSettings"/>.
///
/// Thêm 28/08/2026: thay cho <c>SmtpEmailService</c> (SMTP thô qua cổng 587) sau khi phát
/// hiện `forgot-password` trên Render mất tới ~2.3 phút mỗi lần gọi — nghẽn ở bước kết nối
/// SMTP ra ngoài, không rõ do IPv6 hay do mạng của Render chặn/làm chậm cổng 587. Gửi qua
/// HTTPS REST API né hẳn lớp socket SMTP nên né luôn cả lớp vấn đề đó, bất kể nguyên nhân
/// thật là gì.
///
/// TUYỆT ĐỐI không đặt ApiKey trong appsettings.json — file đó được commit.
///
/// Ví dụ (chuột phải project ADSUS_BE &gt; Manage User Secrets):
/// <code>
/// "Resend": {
///   "ApiKey": "re_xxxxxxxxxxxxxxxxxxxxxxxxxxxx",
///   "FromAddress": "onboarding@resend.dev",
///   "FromName": "ADSUS"
/// }
/// </code>
///
/// Lưu ý: domain mặc định <c>resend.dev</c> chỉ dùng được lúc dev/test (Resend giới hạn gửi
/// tới đúng email đã đăng ký tài khoản). Gửi thật cho người dùng bất kỳ cần verify 1 domain
/// riêng trong dashboard Resend rồi đổi FromAddress sang domain đó.
/// </summary>
public class ResendSettings
{
    public const string SectionName = "Resend";

    /// <summary>API key của Resend. Bỏ trống nghĩa là chưa cấu hình Resend.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Địa chỉ hiện ở ô "Từ". Phải thuộc domain đã verify trên Resend (hoặc resend.dev lúc test).</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Tên hiển thị của người gửi.</summary>
    public string FromName { get; set; } = "ADSUS";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
