namespace ADSUS_BE;

/// <summary>
/// Tên các chính sách giới hạn tần suất. Để riêng ở đây vì cả Program.cs (nơi khai) lẫn
/// controller (nơi gắn thuộc tính) đều cần — gõ tay hai lần là sai chính tả một cái thì
/// build vẫn qua nhưng chính sách im lặng không có tác dụng.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>
    /// Cho các endpoint không cần đăng nhập mà đụng tới mật khẩu: đăng nhập và quên mật khẩu.
    /// </summary>
    public const string Auth = "auth";
}
