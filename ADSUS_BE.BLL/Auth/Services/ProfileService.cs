using System.Globalization;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;

namespace ADSUS_BE.BLL.Auth.Services;

public class ProfileService : IProfileService
{
    private const string DateFormat = "yyyy-MM-dd";

    private readonly IUserRepository _users;

    public ProfileService(IUserRepository users) => _users = users;

    public async Task<UserProfileResponse?> GetOwnProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null) return null;

        return new UserProfileResponse
        {
            FullName = user.FullName,
            PhoneNumber = user.Phone,
            Email = user.Email,
            DateOfBirth = user.DateOfBirth?.ToString(DateFormat, CultureInfo.InvariantCulture),
            Role = user.Role.ToApiString(),
            BiometricEnabled = user.BiometricEnabled,
        };
    }

    public async Task<ProfileOperationResult> UpdateOwnProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null) return ProfileOperationResult.UserNotFound;

        if (user.Status != UserStatus.Active) return ProfileOperationResult.AccountNotActive;

        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();

        // DB có unique index trên lower(email). Kiểm trước để trả thông báo tử tế thay vì
        // để lỗi vi phạm ràng buộc bắn lên từ tầng dưới.
        if (email is not null
            && await _users.IsEmailUsedByAnotherUserAsync(userId, email, cancellationToken))
        {
            return ProfileOperationResult.EmailAlreadyUsed;
        }

        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.DateOfBirth = ParseDateOrNull(request.DateOfBirth);
        user.UpdatedAt = DateTime.UtcNow;

        // BR-02: KHÔNG gán lại user.Phone — số điện thoại là định danh đăng nhập, chỉ phòng
        // khám đổi được.
        // BR-03: không chạm tới bất kỳ dữ liệu y tế nào; entity User vốn không chứa dữ liệu đó.

        await _users.SaveChangesAsync(cancellationToken);

        return ProfileOperationResult.Success;
    }

    public async Task<ProfileOperationResult> SetBiometricEnabledAsync(
        Guid userId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null) return ProfileOperationResult.UserNotFound;

        if (user.Status != UserStatus.Active) return ProfileOperationResult.AccountNotActive;

        user.BiometricEnabled = enabled;
        user.UpdatedAt = DateTime.UtcNow;

        await _users.SaveChangesAsync(cancellationToken);

        return ProfileOperationResult.Success;
    }

    private static DateOnly? ParseDateOrNull(string? value) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? date
            : null;
}
