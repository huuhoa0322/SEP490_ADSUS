namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Bổ sung cột <c>status</c> mà scaffold không sinh được (enum PostgreSQL).
///
/// Để trong lớp partial riêng nên chạy lại <c>scaffold --force</c> cũng không mất — file
/// Prescription.cs sinh tự động sẽ bị ghi đè, file này thì không.
///
/// Completed suy ra khi mọi liều thuộc đơn đã Taken (UC-17).
/// </summary>
public partial class Prescription
{
    public PrescriptionStatus Status { get; set; } = PrescriptionStatus.Active;
}
