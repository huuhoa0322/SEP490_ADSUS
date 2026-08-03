namespace ADSUS_BE.BLL.MedicalRecord.DTOs;

/// <summary>
/// Một file người dùng tải lên, không phụ thuộc ASP.NET Core.
///
/// ADSUS_BE.BLL là class library thuần nên không thấy IFormFile. Controller quy đổi sang
/// kiểu này trước khi gọi Service — nhờ vậy tầng nghiệp vụ không dính kiểu của tầng web.
/// </summary>
public sealed record UploadedFile(
    string FileName,
    string ContentType,
    long Length,
    Stream Content);
