using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.BLL.PatientRelationship.DTOs;
using ADSUS_BE.BLL.PatientRelationship.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;

using PatientRelEntity = ADSUS_BE.DAL.Entities.PatientRelationship;

namespace ADSUS_BE.BLL.PatientRelationship.Services;

/// <summary>
/// Implementation của IPatientRelationshipService. Hồ sơ người thân (guest profile) thuộc module
/// MedicalRecord nên đọc/tạo/sửa qua IPatientProfileService; transaction mở qua IUnitOfWork —
/// service không cầm AppDbContext (P11 review 24/09/2026).
/// </summary>
public sealed class PatientRelationshipService : IPatientRelationshipService
{
    private readonly IPatientRelationshipRepository _repository;
    private readonly IPatientProfileService _patientProfiles;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;

    public PatientRelationshipService(
        IPatientRelationshipRepository repository,
        IPatientProfileService patientProfiles,
        IUserRepository users,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _patientProfiles = patientProfiles;
        _users = users;
        _unitOfWork = unitOfWork;
    }

    public async Task<RelativesListResponse> GetRelativesAsync(Guid userId, CancellationToken ct = default)
    {
        var relationships = await _repository.GetByUserIdAsync(userId, ct);

        var responses = relationships.Select(MapToResponse).ToList();

        return new RelativesListResponse(responses);
    }

    public async Task<RelativeResponse?> GetRelativeByIdAsync(
        Guid relationshipId,
        Guid userId,
        CancellationToken ct = default)
    {
        var relationship = await _repository.GetByIdAndUserAsync(relationshipId, userId, ct);

        return relationship == null ? null : MapToResponse(relationship);
    }

    public async Task<RelativeResponse> AddRelativeAsync(
        AddRelativeRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        var hasPhone = !string.IsNullOrWhiteSpace(request.Phone);
        var normalizedPhone = hasPhone ? request.Phone!.Trim() : null;

        if (hasPhone)
        {
            // Kiểm tra SĐT đã có tài khoản User chưa
            var phoneExists = await _repository.IsPhoneRegisteredAsync(normalizedPhone!, ct);
            if (phoneExists)
            {
                throw new InvalidOperationException(
                    "Số điện thoại này đã có tài khoản trong hệ thống. Người thân vui lòng đăng nhập bằng tài khoản riêng để đặt lịch.");
            }
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // Guest profile trùng số điện thoại (nếu có) hoặc guest profile mới — chưa lưu
            var profile = await _patientProfiles.StageGuestProfileAsync(
                request.FullName, normalizedPhone, request.DateOfBirth, userId, ct);

            // Kiem tra relationship chua ton tai
            if (await _repository.ExistsAsync(userId, profile.PatientProfileId, ct))
            {
                throw new InvalidOperationException("Người thân này đã có trong danh bạ của bạn.");
            }

            // Tao relationship
            var relationship = new PatientRelEntity
            {
                RelationshipId = Guid.NewGuid(),
                UserId = userId,
                PatientProfileId = profile.PatientProfileId,
                RelationshipName = request.RelationshipName,
                CreatedAt = DateTime.UtcNow,
            };
            await _repository.StageAddAsync(relationship, ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return MapToResponse(relationship, profile);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<RelativeResponse?> AddRelativeForGuardianAsync(
        AddRelativeRequest request,
        Guid guardianUserId,
        CancellationToken ct = default)
    {
        var guardian = await _users.GetByIdReadOnlyAsync(guardianUserId, ct);
        if (guardian == null || guardian.Role != UserRole.Patient)
        {
            return null;
        }

        return await AddRelativeAsync(request, guardianUserId, ct);
    }

    public async Task<RelativeResponse> UpdateRelativeAsync(
        Guid relationshipId,
        UpdateRelativeRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        // Bản CÓ tracking để EF lưu được RelationshipName
        var relationship = await _repository.GetByIdAndUserForUpdateAsync(relationshipId, userId, ct);

        if (relationship == null)
        {
            throw new KeyNotFoundException("Relationship not found.");
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // 1. Cập nhật RelationshipName trên entity PatientRelationship
            if (request.RelationshipName != null)
            {
                relationship.RelationshipName = request.RelationshipName;
            }

            // 2. Cập nhật FullName/Phone/DateOfBirth trên PatientProfile
            //    (chỉ khi profile là guest — UserId IS NULL)
            var guestProfile = await _patientProfiles.FindGuestProfileAsync(relationship.PatientProfileId, ct);
            if (guestProfile != null)
            {
                if (request.Phone != null)
                {
                    var normalizedPhone = request.Phone.Trim();
                    if (!string.IsNullOrEmpty(normalizedPhone) && normalizedPhone != guestProfile.Phone)
                    {
                        // Kiểm tra SĐT mới có trùng tài khoản đã đăng ký không
                        var phoneExists = await _repository.IsPhoneRegisteredAsync(normalizedPhone, ct);
                        if (phoneExists)
                        {
                            throw new InvalidOperationException(
                                "Số điện thoại này đã có tài khoản trong hệ thống. Người thân vui lòng đăng nhập bằng tài khoản riêng để đặt lịch.");
                        }
                    }
                }

                await _patientProfiles.StageGuestProfileUpdateAsync(
                    relationship.PatientProfileId, request.FullName, request.Phone, request.DateOfBirth, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            // Load lại để map response — bản CÓ Include hồ sơ + tài khoản. Trước đây dùng
            // GetByIdAsync (không Include) nên response sau khi sửa luôn trả tên rỗng, sđt null.
            var loaded = await _repository.GetByIdAndUserAsync(relationshipId, userId, ct);
            return MapToResponse(loaded!);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public Task DeleteRelativeAsync(
        Guid relationshipId,
        Guid userId,
        CancellationToken ct = default)
    {
        throw new InvalidOperationException("Không được phép xóa người thân để bảo đảm tính toàn vẹn của hồ sơ và lịch sử ca khám bệnh.");
    }

    public async Task<bool> IsPhoneRegisteredAsync(string phone, CancellationToken ct = default)
    {
        return await _repository.IsPhoneRegisteredAsync(phone, ct);
    }

    public async Task<RelationshipBookingTarget?> FindBookingTargetAsync(
        Guid relationshipId, Guid? ownerUserId, CancellationToken ct = default)
    {
        var relationship = ownerUserId.HasValue
            ? await _repository.GetByIdAndUserAsync(relationshipId, ownerUserId.Value, ct)
            : await _repository.GetByIdAsync(relationshipId, ct);

        return relationship is null
            ? null
            : new RelationshipBookingTarget(relationship.RelationshipId, relationship.PatientProfileId, relationship.UserId);
    }

    private static RelativeResponse MapToResponse(DAL.Entities.PatientRelationship relationship)
    {
        var profile = relationship.PatientProfile;
        var user = profile?.User;

        return new RelativeResponse(
            relationship.RelationshipId,
            relationship.PatientProfileId,
            user?.FullName ?? profile?.FullName ?? string.Empty,
            user?.Phone ?? profile?.Phone,
            profile?.DateOfBirth,
            user?.Gender?.ToString(),
            relationship.RelationshipName,
            user != null,
            relationship.CreatedAt
        );
    }

    /// <summary>Người thân vừa thêm luôn là guest profile (chưa có tài khoản) — thông tin nằm trên hồ sơ.</summary>
    private static RelativeResponse MapToResponse(PatientRelEntity relationship, GuestProfileInfo profile) =>
        new(
            relationship.RelationshipId,
            relationship.PatientProfileId,
            profile.FullName ?? string.Empty,
            profile.Phone,
            profile.DateOfBirth,
            null,
            relationship.RelationshipName,
            false,
            relationship.CreatedAt);
}
