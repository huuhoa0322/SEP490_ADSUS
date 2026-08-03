namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Bổ sung cột <c>status</c> mà scaffold không sinh được (enum PostgreSQL).
///
/// Để trong lớp partial riêng nên chạy lại <c>scaffold --force</c> cũng không mất — file
/// AiModelVersion.cs sinh tự động sẽ bị ghi đè, file này thì không.
///
/// Chỉ 1 phiên bản ACTIVE tại một thời điểm (UC-20) — kích hoạt bản mới tự chuyển bản đang
/// ACTIVE về INACTIVE.
/// </summary>
public partial class AiModelVersion
{
    public ModelVersionStatus Status { get; set; } = ModelVersionStatus.Inactive;
}
