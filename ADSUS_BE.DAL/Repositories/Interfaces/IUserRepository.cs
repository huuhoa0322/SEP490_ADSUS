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
    /// Đọc tài khoản để SỬA — có tracking, khác GetByIdAsync (AsNoTracking, chỉ để hiển thị).
    /// Dùng ở UC-06 AF-02 khi Điều dưỡng sửa lỗi nhập liệu trên tài khoản Bệnh nhân.
    /// </summary>
    Task<User?> GetForUpdateAsync(Guid userId, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// UC-04 BR-02 — số điện thoại là định danh đăng nhập duy nhất của toàn hệ thống,
    /// hai tài khoản không bao giờ được trùng. Kiểm trước khi tạo để báo lỗi tử tế thay vì
    /// để DB ném ra lỗi vi phạm ràng buộc.
    /// </summary>
    Task<bool> PhoneExistsAsync(string phone, CancellationToken cancellationToken = default);

    /// <summary>Kiểm email đã có tài khoản nào dùng chưa. Dùng lúc TẠO MỚI (chưa có id để loại trừ).</summary>
    Task<bool> IsEmailUsedAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Thêm tài khoản mới vào ngữ cảnh. Phải gọi SaveChangesAsync sau đó.</summary>
    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// UC-04 / SCR-06 — danh sách tài khoản cho Admin, có tìm kiếm và lọc.
    ///
    /// <paramref name="keyword"/> khớp không phân biệt hoa thường trên họ tên hoặc số điện thoại.
    /// Trả về cả tổng số bản ghi để giao diện dựng phân trang.
    /// </summary>
    Task<(IReadOnlyList<User> Items, int TotalCount)> SearchAsync(
        string? keyword,
        UserRole? role,
        UserStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// UC-07 GB-04 — danh sách Bác sĩ để chọn người phụ trách ca khám.
    ///
    /// Chỉ tài khoản Active: giao ca cho một bác sĩ đã bị khoá hoặc vô hiệu hoá là tạo ra
    /// một ca không ai xử lý được. Không phân trang — số bác sĩ trong một phòng khám là tập
    /// nhỏ có biên, không phải collection tăng trưởng vô hạn.
    /// </summary>
    Task<IReadOnlyList<User>> ListActiveDoctorsAsync(CancellationToken cancellationToken = default);
}
