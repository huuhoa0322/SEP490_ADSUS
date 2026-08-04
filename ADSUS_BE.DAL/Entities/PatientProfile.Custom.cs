namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Bổ sung cột <c>gender</c> mà scaffold không sinh được (enum PostgreSQL).
///
/// Để trong lớp partial riêng nên chạy lại <c>scaffold --force</c> cũng không mất — file
/// PatientProfile.cs sinh tự động sẽ bị ghi đè, file này thì không.
///
/// Thuộc tính nghiệp vụ của Patient Profile (PRD §2.2.b): giới tính, tiền sử bệnh, dị ứng,
/// bác sĩ lập hồ sơ.
/// </summary>
public partial class PatientProfile
{
    public GenderType Gender { get; set; } = GenderType.Female;
}
