namespace ADSUS_BE.BLL.Auth.DTOs;

/// <summary>
/// Outcome of a change-password attempt.
///
/// Unlike sign-in, naming the exact reason is fine here: the caller is already
/// authenticated and acting on their own account, so nothing is disclosed. The
/// error-masking rule from GB-06 applies to the sign-in screen only.
/// </summary>
public enum ChangePasswordResult
{
    Success,

    /// <summary>AF-01 — the supplied current password does not match.</summary>
    CurrentPasswordIncorrect,

    /// <summary>Token is valid but the account no longer exists (deleted manually, for example).</summary>
    UserNotFound,

    /// <summary>The account was locked or deactivated after the token was issued.</summary>
    AccountNotActive,
}
