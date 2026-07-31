using ADSUS_BE.BLL.UserRoleManagement.DTOs;

namespace ADSUS_BE.BLL.UserRoleManagement.Interfaces;

/// <summary>
/// UC-03 FT-06 — cấp lại mật khẩu. Có hai đường: người dùng tự làm, và Admin làm hộ.
/// </summary>
public interface IPasswordResetService
{
    /// <summary>
    /// Đường tự phục vụ, gọi từ màn đăng nhập.
    ///
    /// TRẢ VỀ void — CÓ CHỦ Ý, không phải quên.
    ///
    /// AF-01 bắt buộc: số điện thoại không tồn tại, email không khớp, hay tài khoản đã bị
    /// khoá đều phải nhận đúng một câu trả lời giống hệt nhau. Kiểu void khiến controller
    /// KHÔNG THỂ phân biệt được các trường hợp đó, nên không thể vô tình làm lộ — cùng cách
    /// ép luật như LoginAsync trả về null ở GB-06.
    ///
    /// Nếu sau này ai đó muốn đổi thành trả về bool "đã gửi hay chưa", hãy đọc lại AF-01
    /// trước: đó chính là lỗ hổng dò tài khoản mà luật này sinh ra để bịt.
    /// </summary>
    Task RequestSelfServiceResetAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// AF-02 — Admin cấp lại hộ từ SCR-06, dùng khi chủ tài khoản không vào được email.
    ///
    /// Ở đây trả về kết quả chi tiết được, vì Admin đã biết tài khoản tồn tại (họ chọn nó từ
    /// danh sách) nên không có gì để lộ thêm.
    ///
    /// BR-03: mật khẩu tạm vẫn chỉ gửi qua email, KHÔNG BAO GIỜ hiện trên màn hình Admin.
    /// </summary>
    Task<AccountOperationResult> AdminResetAsync(
        Guid userId,
        Guid actingAdminId,
        CancellationToken cancellationToken = default);
}
