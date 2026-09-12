namespace ADSUS_BE.BLL.Common;

/// <summary>
/// Cấu hình gửi SMS qua eSMS.vn (REST API qua HTTPS), đọc từ User Secrets — cùng cách chia
/// với <see cref="SendGridSettings"/>.
///
/// TUYỆT ĐỐI không đặt ApiKey/SecretKey trong appsettings.json — file đó được commit.
///
/// Ví dụ (chuột phải project ADSUS_BE &gt; Manage User Secrets):
/// <code>
/// "Esms": {
///   "ApiKey": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
///   "SecretKey": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
///   "Brandname": "ADSUS"
/// }
/// </code>
/// </summary>
public class EsmsSettings
{
    public const string SectionName = "Esms";

    /// <summary>API key của eSMS.vn. Bỏ trống nghĩa là chưa cấu hình.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Secret key của eSMS.vn (khác ApiKey — eSMS cấp 2 khoá riêng).</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Tên brandname đã đăng ký với eSMS. Rỗng thì eSMS dùng đầu số mặc định của tài khoản.</summary>
    public string Brandname { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(SecretKey);
}
