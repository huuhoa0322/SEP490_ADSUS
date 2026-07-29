using ADSUS_BE.BLL.Auth.DTOs;

namespace ADSUS_BE.BLL.Auth.Interfaces;

public interface IAuthService
{
    /// <summary>
    /// UC-01 — sign in.
    /// Returns null for EVERY failure: unknown phone number, wrong password, locked account,
    /// deactivated account.
    ///
    /// The return type is deliberate. UCS GB-06 requires every sign-in failure to produce an
    /// identical message with no hint of the real cause. Returning a detailed error code
    /// would sooner or later leak into a response by accident; if the information does not
    /// exist in the return type, it cannot leak.
    /// </summary>
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// UC-25 — a signed-in user changes their own password.
    /// A successful change also clears the mandatory-change flag if one was set.
    /// </summary>
    Task<ChangePasswordResult> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);
}
