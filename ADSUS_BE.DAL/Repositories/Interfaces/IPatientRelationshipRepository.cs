using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Repository cho PatientRelationship - quản lý danh bạ người thân của user.
/// </summary>
public interface IPatientRelationshipRepository
{
    /// <summary>
    /// Lấy danh sách người thân của một user.
    /// </summary>
    Task<IReadOnlyList<PatientRelationship>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Lấy chi tiết một relationship (có Include navigation).
    /// </summary>
    Task<PatientRelationship?> GetByIdAsync(Guid relationshipId, CancellationToken ct = default);

    /// <summary>
    /// Lấy relationship theo user + relationshipId (để kiểm tra ownership).
    /// </summary>
    Task<PatientRelationship?> GetByIdAndUserAsync(Guid relationshipId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra xem user đã lưu patient_profile này chưa.
    /// </summary>
    Task<bool> ExistsAsync(Guid userId, Guid patientProfileId, CancellationToken ct = default);

    /// <summary>Relationship của đúng user sở hữu — CÓ tracking, không kèm navigation (sửa tên quan hệ).</summary>
    Task<PatientRelationship?> GetByIdAndUserForUpdateAsync(Guid relationshipId, Guid userId, CancellationToken ct = default);

    /// <summary>Thêm relationship vào context, CHƯA lưu — khác <see cref="AddAsync"/> (lưu ngay).</summary>
    Task StageAddAsync(PatientRelationship relationship, CancellationToken ct = default);

    /// <summary>
    /// Thêm relationship mới.
    /// </summary>
    Task<PatientRelationship> AddAsync(PatientRelationship relationship, CancellationToken ct = default);

    /// <summary>
    /// Cập nhật relationship (chỉ RelationshipName).
    /// </summary>
    Task UpdateAsync(PatientRelationship relationship, CancellationToken ct = default);

    /// <summary>
    /// Xóa relationship.
    /// </summary>
    Task DeleteAsync(Guid relationshipId, CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra SĐT đã được đăng ký tài khoản chưa (cho Account Linking).
    /// </summary>
    Task<bool> IsPhoneRegisteredAsync(string phone, CancellationToken ct = default);
}
