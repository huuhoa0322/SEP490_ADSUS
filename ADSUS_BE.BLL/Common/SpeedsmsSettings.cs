namespace ADSUS_BE.BLL.Common;

/// <summary>
/// Cấu hình gửi SMS qua Speedsms.vn (REST API qua HTTPS), đọc từ User Secrets — cùng cách
/// chia với <see cref="SendGridSettings"/>.
///
/// Thay eSMS.vn (13/09/2026) — dùng dịch vụ "Verify" của Speedsms (sms_type = 4), brandname
/// mặc định do Speedsms cấp sẵn ("Verify"/"Notify"). KHÔNG cần đăng ký brandname riêng với nhà
/// mạng, nên KHÔNG cần Giấy phép Đăng ký Kinh doanh — khác eSMS Brandname (sms_type = 2), vốn
/// bắt buộc phải có giấy phép này để đăng ký tên hiển thị riêng.
///
/// TUYỆT ĐỐI không đặt AccessToken trong appsettings.json — file đó được commit.
///
/// Ví dụ (chuột phải project ADSUS_BE &gt; Manage User Secrets):
/// <code>
/// "Speedsms": {
///   "AccessToken": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
/// }
/// </code>
///
/// Lấy Access Token: đăng ký tài khoản tại connect.speedsms.vn, đăng nhập rồi vào
/// Cài đặt &gt; Hồ sơ.
/// </summary>
public class SpeedsmsSettings
{
    public const string SectionName = "Speedsms";

    /// <summary>Access token của Speedsms.vn. Bỏ trống nghĩa là chưa cấu hình.</summary>
    public string AccessToken { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(AccessToken);
}
