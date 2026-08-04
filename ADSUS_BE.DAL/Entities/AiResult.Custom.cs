namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Bổ sung cột <c>status</c> mà scaffold không sinh được (enum PostgreSQL).
///
/// Để trong lớp partial riêng nên chạy lại <c>scaffold --force</c> cũng không mất — file
/// AiResult.cs sinh tự động sẽ bị ghi đè, file này thì không.
///
/// Bảng ai_results thuộc Module 5. Ở đây chỉ THÊM thuộc tính để Dashboard (UC-05) đếm được
/// tỉ lệ Confirmed/Rejected, không đổi gì khác.
/// </summary>
public partial class AiResult
{
    public AiResultStatus Status { get; set; }
}
