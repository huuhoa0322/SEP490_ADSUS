namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Bổ sung computed properties cho PatientRelationship.
/// </summary>
public partial class PatientRelationship
{
    /// <summary>
    /// Tên hiển thị của bệnh nhân (ưu tiên User.FullName > PatientProfile.FullName).
    /// </summary>
    public string PatientDisplayName =>
        PatientProfile?.User?.FullName ?? PatientProfile?.FullName ?? string.Empty;

    /// <summary>
    /// Số điện thoại của bệnh nhân (ưu tiên User.Phone > PatientProfile.Phone).
    /// </summary>
    public string PatientPhone =>
        PatientProfile?.User?.Phone ?? PatientProfile?.Phone ?? string.Empty;
}
