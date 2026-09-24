using ADSUS_BE.BLL.PatientRelationship.DTOs;

namespace ADSUS_BE.BLL.PatientRelationship.Interfaces;

/// <summary>
/// Service interface cho PatientRelationship - quản lý danh bạ người thân.
/// </summary>
public interface IPatientRelationshipService
{
    /// <summary>
    /// Lấy danh sách người thân của user.
    /// </summary>
    Task<RelativesListResponse> GetRelativesAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Lấy chi tiết một người thân theo relationshipId.
    /// Trả null nếu không tìm thấy hoặc không thuộc user.
    /// </summary>
    Task<RelativeResponse?> GetRelativeByIdAsync(Guid relationshipId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Thêm người thân mới.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Khi patient profile không tồn tại hoặc đã được thêm trước đó.
    /// </exception>
    Task<RelativeResponse> AddRelativeAsync(AddRelativeRequest request, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Nhân viên thêm người thân cho một tài khoản bệnh nhân (người giám hộ). Trả null nếu
    /// <paramref name="guardianUserId"/> không phải tài khoản PATIENT.
    /// </summary>
    Task<RelativeResponse?> AddRelativeForGuardianAsync(AddRelativeRequest request, Guid guardianUserId, CancellationToken ct = default);

    /// <summary>
    /// Cập nhật nhãn người thân.
    /// </summary>
    /// <exception cref="KeyNotFoundException">
    /// Khi relationship không tồn tại hoặc không thuộc user.
    /// </exception>
    Task<RelativeResponse> UpdateRelativeAsync(Guid relationshipId, UpdateRelativeRequest request, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Xóa người thân.
    /// </summary>
    /// <exception cref="KeyNotFoundException">
    /// Khi relationship không tồn tại hoặc không thuộc user.
    /// </exception>
    Task DeleteRelativeAsync(Guid relationshipId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra SĐT đã có tài khoản chưa (cho Account Linking).
    /// </summary>
    Task<bool> IsPhoneRegisteredAsync(string phone, CancellationToken ct = default);

    /// <summary>
    /// Mối quan hệ để đặt lịch hộ (AppointmentService). <paramref name="ownerUserId"/> khác null
    /// thì mối quan hệ PHẢI thuộc danh bạ của tài khoản đó (bệnh nhân tự đặt hộ); null thì chỉ
    /// tìm theo Id (Điều dưỡng đặt hộ tại quầy). Không tìm thấy → null.
    /// </summary>
    Task<RelationshipBookingTarget?> FindBookingTargetAsync(
        Guid relationshipId, Guid? ownerUserId, CancellationToken ct = default);
}
