namespace ADSUS_BE.BLL.Common;

/// <summary>
/// Cấu hình gửi mail qua SendGrid (REST API qua HTTPS), đọc từ User Secrets — cùng cách chia
/// với <see cref="EmailSettings"/>/<see cref="ResendSettings"/>.
///
/// Thêm 28/08/2026: lựa chọn thứ 3 bên cạnh Resend/SMTP. Khác Resend (bắt verify cả 1 domain
/// mới gửi được cho người nhận bất kỳ), SendGrid ở free tier chỉ cần verify ĐÚNG 1 địa chỉ
/// email gửi ("Single Sender Verification" — bấm link xác nhận trong hộp thư của chính địa
/// chỉ đó trên trang SendGrid), không cần quyền quản trị DNS của một domain riêng.
///
/// TUYỆT ĐỐI không đặt ApiKey trong appsettings.json — file đó được commit.
///
/// Ví dụ (chuột phải project ADSUS_BE &gt; Manage User Secrets):
/// <code>
/// "SendGrid": {
///   "ApiKey": "SG.xxxxxxxxxxxxxxxxxxxxxx",
///   "FromAddress": "adsus.noreply@gmail.com",
///   "FromName": "ADSUS"
/// }
/// </code>
///
/// Lưu ý: FromAddress PHẢI là địa chỉ đã verify ở SendGrid Dashboard &gt; Settings &gt; Sender
/// Authentication &gt; Single Sender Verification — gửi bằng địa chỉ chưa verify sẽ bị SendGrid
/// từ chối thẳng.
/// </summary>
public class SendGridSettings
{
    public const string SectionName = "SendGrid";

    /// <summary>API key của SendGrid. Bỏ trống nghĩa là chưa cấu hình SendGrid.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Địa chỉ hiện ở ô "Từ". Phải là địa chỉ đã qua Single Sender Verification.</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Tên hiển thị của người gửi.</summary>
    public string FromName { get; set; } = "ADSUS";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
