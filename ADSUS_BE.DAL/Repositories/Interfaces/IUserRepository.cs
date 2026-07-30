using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

public interface IUserRepository
{
    /// <summary>
    /// Tìm tài khoản theo số điện thoại — định danh đăng nhập duy nhất của hệ thống (BR-02).
    /// Trả về null nếu không tồn tại; tầng nghiệp vụ tự quyết cách phản hồi.
    /// </summary>
    Task<User?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm tài khoản theo khoá chính. Dùng khi đã biết người gọi là ai qua claim trong JWT.
    /// </summary>
    Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra email đã bị tài khoản KHÁC dùng chưa. So sánh không phân biệt hoa thường
    /// để khớp với unique index uq_users_email_lower trong DB.
    ///
    /// Kiểm ở tầng nghiệp vụ để trả về thông báo tử tế, thay vì để DB ném ra lỗi
    /// vi phạm ràng buộc.
    /// </summary>
    Task<bool> IsEmailUsedByAnotherUserAsync(
        Guid userId,
        string email,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
