using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.MedicalRecord.DTOs;

namespace ADSUS_BE.BLL.MedicalRecord.Services;

/// <summary>
/// UC-07 BR-01 — chỉ nhận JPEG hoặc PNG, tối đa 20MB mỗi file (PRD §6.1).
///
/// Nhận dạng bằng magic bytes chứ KHÔNG bằng đuôi file hay header Content-Type: cả hai thứ
/// đó do client tự khai, đổi tên virus.exe thành anh.jpg là qua được ngay.
/// </summary>
public static class UltrasoundImageContentValidator
{
    public const long MaxFileSizeBytes = 20L * 1024 * 1024;

    private static readonly byte[] JpegMagic = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <summary>
    /// Trả về Content-Type chuẩn suy ra từ nội dung file. Ném BusinessException (→ 422) nếu
    /// file sai định dạng hoặc quá lớn (UC-07 AF-01).
    /// </summary>
    public static async Task<string> ValidateAndResolveContentTypeAsync(
        UploadedFile file,
        CancellationToken ct = default)
    {
        if (file.Length <= 0)
        {
            throw new BusinessException($"File '{file.FileName}' is empty.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new BusinessException(
                $"File '{file.FileName}' is larger than the 20MB limit.");
        }

        var header = new byte[8];
        var totalRead = 0;

        // Stream.ReadAsync có thể trả về ít hơn số byte yêu cầu dù chưa hết luồng — phải lặp
        // đến khi đầy buffer hoặc gặp EOF thật sự (đọc được 0 byte), không được tin một lần gọi.
        while (totalRead < header.Length)
        {
            var read = await file.Content.ReadAsync(header.AsMemory(totalRead, header.Length - totalRead), ct);
            if (read == 0) break;
            totalRead += read;
        }

        // Trả con trỏ về đầu, nếu không thì lát nữa upload sẽ đẩy lên thiếu 8 byte đầu.
        if (file.Content.CanSeek)
        {
            file.Content.Seek(0, SeekOrigin.Begin);
        }

        if (totalRead >= 3 && StartsWith(header, JpegMagic))
        {
            return "image/jpeg";
        }

        if (totalRead >= 8 && StartsWith(header, PngMagic))
        {
            return "image/png";
        }

        throw new BusinessException(
            $"File '{file.FileName}' is not a JPEG or PNG image.");
    }

    private static bool StartsWith(byte[] buffer, byte[] prefix)
    {
        for (var i = 0; i < prefix.Length; i++)
        {
            if (buffer[i] != prefix[i]) return false;
        }

        return true;
    }
}
