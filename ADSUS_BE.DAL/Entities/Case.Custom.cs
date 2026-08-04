namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Bổ sung cột <c>status</c> mà scaffold không sinh được (enum PostgreSQL).
///
/// Để trong lớp partial riêng nên chạy lại <c>scaffold --force</c> cũng không mất — file
/// Case.cs sinh tự động sẽ bị ghi đè, file này thì không.
///
/// Vòng đời một chiều: Created → Analyzed → Confirmed (UC-19).
/// </summary>
public partial class Case
{
    public CaseStatus Status { get; set; } = CaseStatus.Created;
}
