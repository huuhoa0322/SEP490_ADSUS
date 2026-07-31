namespace ADSUS_BE.BLL.Auth.DTOs;

/// <summary>
/// Kết quả các thao tác trên hồ sơ cá nhân (UC-10) và bật/tắt sinh trắc học (UC-02).
///
/// Giống UC-25, ở đây được phép nói rõ nguyên nhân vì người gọi đã đăng nhập và đang thao
/// tác trên chính tài khoản mình. Luật che giấu nguyên nhân GB-06 chỉ áp cho màn đăng nhập.
/// </summary>
public enum ProfileOperationResult
{
    Success,

    /// <summary>Token hợp lệ nhưng tài khoản không còn trong DB.</summary>
    UserNotFound,

    /// <summary>Tài khoản bị khoá hoặc vô hiệu hoá sau khi token được cấp.</summary>
    AccountNotActive,

    /// <summary>Email mới trùng với tài khoản khác — DB có unique index trên lower(email).</summary>
    EmailAlreadyUsed,
}
