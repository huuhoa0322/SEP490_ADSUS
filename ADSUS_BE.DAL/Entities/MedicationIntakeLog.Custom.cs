namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Bổ sung cột <c>status</c> mà scaffold không sinh được (enum PostgreSQL).
///
/// Để trong lớp partial riêng nên chạy lại <c>scaffold --force</c> cũng không mất — file
/// MedicationIntakeLog.cs sinh tự động sẽ bị ghi đè, file này thì không.
///
/// Không có "Missed" — JOB-01 nhắc lặp lại liên tục cho tới khi bệnh nhân xác nhận Taken.
/// </summary>
public partial class MedicationIntakeLog
{
    public IntakeStatus Status { get; set; } = IntakeStatus.Pending;
}
