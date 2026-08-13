using System.Globalization;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Auth.Mappers;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.Auth.Services;

public class ProfileService : IProfileService
{
    private const string DateFormat = "yyyy-MM-dd";

    private readonly IUserRepository _users;
    private readonly ILogger<ProfileService> _logger;

    public ProfileService(IUserRepository users, ILogger<ProfileService> logger)
    {
        _users = users;
        _logger = logger;
    }

    public async Task<UserProfileResponse?> GetOwnProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Chỉ đọc để hiển thị lên SCR-03, không sửa/lưu gì ở đây — dùng bản AsNoTracking
        // (P11 review Module 1, 12/08/2026).
        var user = await _users.GetByIdReadOnlyAsync(userId, cancellationToken);

        // Tài khoản không tồn tại hoặc đã bị khoá/vô hiệu hoá đều trả null như nhau, để
        // controller trả về đúng một câu 401 (GB-06).
        //
        // Trên thực tế tầng xác thực (AccountStatusJwtEvents) đã chặn từ trước, nên nhánh
        // này gần như không chạy tới. Vẫn giữ để service tự đứng vững được: ai gọi thẳng
        // service mà quên đi qua tầng xác thực thì cũng không lọt.
        if (user is null || user.Status != UserStatus.Active) return null;

        return UserMapper.ToProfileResponse(user);
    }

    public async Task<ProfileOperationResult> UpdateOwnProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetForUpdateAsync(userId, cancellationToken);
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

        _logger.LogInformation("User {UserId} updated their profile successfully", userId);

        return ProfileOperationResult.Success;
    }

    public async Task<ProfileOperationResult> SetBiometricEnabledAsync(
        Guid userId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetForUpdateAsync(userId, cancellationToken);
        if (user is null) return ProfileOperationResult.UserNotFound;

        if (user.Status != UserStatus.Active) return ProfileOperationResult.AccountNotActive;

        user.BiometricEnabled = enabled;
        user.UpdatedAt = DateTime.UtcNow;

        await _users.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User {UserId} set biometric sign-in to {Enabled}", userId, enabled);

        return ProfileOperationResult.Success;
    }

    private static DateOnly? ParseDateOrNull(string? value) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? date
            : null;
}
