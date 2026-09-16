using ADSUS_BE.BLL.PatientRelationship.DTOs;
using ADSUS_BE.BLL.PatientRelationship.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

using PatientRelEntity = ADSUS_BE.DAL.Entities.PatientRelationship;

namespace ADSUS_BE.BLL.PatientRelationship.Services;

/// <summary>
/// Implementation của IPatientRelationshipService.
/// </summary>
public sealed class PatientRelationshipService : IPatientRelationshipService
{
    private readonly IPatientRelationshipRepository _repository;
    private readonly IPatientProfileRepository _patientProfileRepository;
    private readonly AppDbContext _context;

    public PatientRelationshipService(
        IPatientRelationshipRepository repository,
        IPatientProfileRepository patientProfileRepository,
        AppDbContext context)
    {
        _repository = repository;
        _patientProfileRepository = patientProfileRepository;
        _context = context;
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

        async Task<RelativeResponse> ExecuteCreationAsync()
        {
            PatientProfile? profile = null;

            if (hasPhone)
            {
                // Tìm guest profile đã tồn tại theo phone (guest profile có UserId == null)
                profile = await _context.PatientProfiles
                    .FirstOrDefaultAsync(p => p.Phone == normalizedPhone && p.UserId == null, ct);
            }

            if (profile == null)
            {
                profile = new PatientProfile
                {
                    PatientProfileId = Guid.NewGuid(),
                    FullName = request.FullName,
                    Phone = normalizedPhone,
                    DateOfBirth = request.DateOfBirth,
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                _context.PatientProfiles.Add(profile);
            }

            // Kiem tra relationship chua ton tai
            var exists = await _context.PatientRelationships
                .AnyAsync(r => r.UserId == userId && r.PatientProfileId == profile.PatientProfileId, ct);
            if (exists)
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
            _context.PatientRelationships.Add(relationship);

            await _context.SaveChangesAsync(ct);
            return MapToResponse(relationship, profile);
        }

        if (_context.Database.IsRelational())
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(ct);
                try
                {
                    var result = await ExecuteCreationAsync();
                    await transaction.CommitAsync(ct);
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync(ct);
                    throw;
                }
            });
        }
        else
        {
            return await ExecuteCreationAsync();
        }
    }

    public async Task<RelativeResponse> UpdateRelativeAsync(
        Guid relationshipId,
        UpdateRelativeRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        // Query trực tiếp từ context WITH TRACKING (không dùng AsNoTracking)
        // để EF Core detect changes khi cập nhật PatientProfile
        var relationship = await _context.Set<PatientRelEntity>()
            .Include(r => r.PatientProfile)
                .ThenInclude(p => p!.User)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.RelationshipId == relationshipId && r.UserId == userId, ct);

        if (relationship == null)
        {
            throw new KeyNotFoundException("Relationship not found.");
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                // 1. Cập nhật RelationshipName trên entity PatientRelationship
                if (request.RelationshipName != null)
                {
                    relationship.RelationshipName = request.RelationshipName;
                }

                // 2. Cập nhật FullName/Phone/DateOfBirth trên PatientProfile
                //    (chỉ khi profile là guest — UserId IS NULL)
                var profile = relationship.PatientProfile;
                if (profile != null && profile.UserId == null)
                {
                    if (request.FullName != null)
                    {
                        profile.FullName = request.FullName.Trim();
                    }

                    if (request.Phone != null)
                    {
                        var normalizedPhone = request.Phone.Trim();
                        if (!string.IsNullOrEmpty(normalizedPhone) && normalizedPhone != profile.Phone)
                        {
                            // Kiểm tra SĐT mới có trùng tài khoản đã đăng ký không
                            var phoneExists = await _repository.IsPhoneRegisteredAsync(normalizedPhone, ct);
                            if (phoneExists)
                            {
                                throw new InvalidOperationException(
                                    "Số điện thoại này đã có tài khoản trong hệ thống. Người thân vui lòng đăng nhập bằng tài khoản riêng để đặt lịch.");
                            }
                        }
                        profile.Phone = string.IsNullOrEmpty(normalizedPhone) ? null : normalizedPhone;
                    }

                    if (request.DateOfBirth != null)
                    {
                        profile.DateOfBirth = request.DateOfBirth;
                    }

                    profile.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                // Load lại để map response
                var loaded = await _repository.GetByIdAsync(relationshipId, ct);
                return MapToResponse(loaded!);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task DeleteRelativeAsync(
        Guid relationshipId,
        Guid userId,
        CancellationToken ct = default)
    {
        var relationship = await _repository.GetByIdAndUserAsync(relationshipId, userId, ct);

        if (relationship == null)
        {
            throw new KeyNotFoundException("Relationship not found.");
        }

        await _repository.DeleteAsync(relationshipId, ct);
    }

    public async Task<bool> IsPhoneRegisteredAsync(string phone, CancellationToken ct = default)
    {
        return await _repository.IsPhoneRegisteredAsync(phone, ct);
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

    private static RelativeResponse MapToResponse(PatientRelEntity relationship, PatientProfile profile)
    {
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
}
