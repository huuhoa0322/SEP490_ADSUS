namespace ADSUS_BE.DAL.ExternalServices;

/// <summary>
/// Cấu hình Supabase Storage. Url/Bucket/Ttl nằm trong appsettings.json, riêng ServiceKey
/// nằm trong User Secrets — cùng cách chia như JwtSettings.
///
/// Đặt ở DAL chứ không phải BLL/Common như các settings khác: BLL tham chiếu DAL, không có
/// chiều ngược lại, nên SupabaseStorageService (nằm ở DAL) không nhìn thấy class ở BLL.
/// </summary>
public class SupabaseStorageSettings
{
    public const string SectionName = "SupabaseStorage";

    /// <summary>Ví dụ https://abcdefgh.supabase.co — không có dấu / ở cuối.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Service-role key. KHÔNG BAO GIỜ commit, KHÔNG BAO GIỜ gửi ra client.</summary>
    public string ServiceKey { get; set; } = string.Empty;

    public string Bucket { get; set; } = "ultrasound-images";

    /// <summary>Hạn của signed URL. Ngắn thôi — ảnh y tế không nên có link sống lâu.</summary>
    public int SignedUrlTtlSeconds { get; set; } = 300;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(ServiceKey);
}
