namespace ADSUS_BE.DAL.ExternalServices;

/// <summary>
/// Lưu trữ file nhị phân ngoài database. File ảnh siêu âm không nằm trong Postgres — bảng
/// ultrasound_images chỉ giữ đường dẫn (file_ref).
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Đẩy file lên bucket. Trả lại chính <paramref name="objectPath"/> để bên gọi lưu vào
    /// cột file_ref. Ném <see cref="InvalidOperationException"/> nếu bucket từ chối.
    /// </summary>
    Task<string> UploadAsync(Stream content, string objectPath, string contentType, CancellationToken ct = default);
    Task<string> UploadAsync(Stream content, string objectPath, string contentType, string bucketName, CancellationToken ct = default);

    /// <summary>
    /// Tạo URL đọc có hạn. Trả <c>null</c> nếu ký thất bại (object không tồn tại, mạng lỗi) —
    /// KHÔNG ném, vì một ảnh mất file không phải lý do để chặn bác sĩ đọc cả hồ sơ.
    /// </summary>
    Task<string?> CreateSignedUrlAsync(string objectPath, CancellationToken ct = default);
    Task<string?> CreateSignedUrlAsync(string objectPath, string bucketName, CancellationToken ct = default);

    /// <summary>
    /// Chỉ dùng để dọn file mồ côi khi ghi database thất bại sau lúc upload. KHÔNG phải chức
    /// năng xoá ảnh của người dùng — GB-03 cấm xoá dữ liệu y tế.
    /// </summary>
    Task DeleteAsync(string objectPath, CancellationToken ct = default);
    Task DeleteAsync(string objectPath, string bucketName, CancellationToken ct = default);
}
