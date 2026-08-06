namespace ADSUS_BE.BLL.UserRoleManagement.DTOs;

/// <summary>
/// Kết quả của một thao tác quản lý tài khoản.
///
/// Trả về enum thay vì ném ngoại lệ, để controller quyết định mã HTTP tương ứng — cùng cách
/// làm với ProfileOperationResult ở Module 1.
/// </summary>
public enum AccountOperationResult
{
    Success,

    /// <summary>Không tìm thấy tài khoản.</summary>
    NotFound,

    /// <summary>BR-02 — số điện thoại đã có tài khoản khác dùng.</summary>
    PhoneAlreadyUsed,

    /// <summary>Email đã có tài khoản khác dùng (DB có unique index trên lower(email)).</summary>
    EmailAlreadyUsed,

    /// <summary>Vai trò gửi lên không hợp lệ, hoặc là ADMIN — không tạo qua màn này được.</summary>
    InvalidRole,

    /// <summary>
    /// Admin đang thao tác lên chính tài khoản mình.
    ///
    /// UC-04 AF-04 để ngỏ trường hợp này; nhóm đã chốt ngày 31/07/2026:
    ///   - Admin ĐƯỢC khoá và vô hiệu hoá Admin khác.
    ///   - Admin KHÔNG được thao tác lên chính mình — làm được thì tự nhốt mình ra ngoài
    ///     hệ thống, và vì không có đường kích hoạt lại (BR-05) nên không ai cứu được.
    /// </summary>
    CannotTargetSelf,

    /// <summary>
    /// BR-05 — tài khoản đã bị vô hiệu hoá thì không quay lại được nữa.
    /// Deactivated là trạng thái cuối, PRD không định nghĩa đường kích hoạt lại.
    /// </summary>
    AccountIsDeactivated,

    /// <summary>
    /// UC-03 AF-02 — tài khoản chưa khai email nên không có chỗ nào để gửi mật khẩu tạm tới.
    ///
    /// KHÔNG còn được AdminResetAsync trả về nữa kể từ quyết định ghi đè 06/08/2026 — trường
    /// hợp không có email giờ vẫn cấp lại thành công, trả plaintext qua
    /// AdminResetOutcome.TemporaryPassword thay vì báo lỗi này. Giữ lại enum member để không
    /// phá vỡ chỗ khác có thể đang tham chiếu.
    /// </summary>
    AccountHasNoEmail,

    /// <summary>
    /// Cố đổi vai trò ADMIN — cả hai chiều: hạ một Admin xuống vai trò khác, hoặc nâng người
    /// khác lên Admin.
    ///
    /// UC-04 ghi rõ Role của màn này chỉ có [Doctor, Nurse, Patient] và "Admin accounts are
    /// not created on this screen". Không chặn thì mở form sửa một tài khoản Admin rồi bấm
    /// Lưu là mất quyền quản trị ngay, vì ô vai trò không có lựa chọn ADMIN nên nó rơi về
    /// giá trị đầu danh sách. Mất Admin cuối cùng là không còn ai tạo lại được.
    /// </summary>
    CannotChangeAdminRole,

    /// <summary>
    /// Tài khoản đã tạo xong nhưng máy chủ mail từ chối, KHÔNG gửi được mật khẩu tạm.
    ///
    /// Không phải lỗi hoàn toàn: bản ghi đã nằm trong database và số điện thoại đã bị chiếm,
    /// nên không được coi như thất bại rồi bảo Admin làm lại — làm lại chỉ nhận được "số điện
    /// thoại đã tồn tại". Phải nói đúng sự thật để Admin biết còn phải cấp lại mật khẩu.
    /// </summary>
    CreatedButEmailNotSent,

    /// <summary>
    /// Tài khoản đã tạo xong nhưng KHÔNG KHAI EMAIL nên chẳng có chỗ nào để gửi mật khẩu tạm.
    ///
    /// UC-04 ghi Email là Optional, cùng lúc lại ghi mật khẩu tạm chỉ đi qua email — hai câu
    /// đó cộng lại thành một tài khoản không ai đăng nhập được. Code làm đúng tài liệu (vẫn
    /// cho tạo) nhưng phải nói rõ để Admin biết còn việc phải làm: bổ sung email rồi cấp lại
    /// mật khẩu. Nhóm nên chốt lại xem Email có nên thành bắt buộc không.
    /// </summary>
    CreatedWithoutEmail,

    /// <summary>
    /// Không gửi được mật khẩu tạm, và mật khẩu cũ ĐƯỢC GIỮ NGUYÊN.
    ///
    /// Dùng cho đường cấp lại mật khẩu. Ở đó thứ tự phải là gửi thư trước rồi mới lưu: đổi
    /// mật khẩu trước mà thư không tới nơi thì mật khẩu cũ đã chết trong khi mật khẩu mới
    /// không ai biết — chủ tài khoản bị nhốt ở ngoài.
    /// </summary>
    EmailNotSent,
}
