using System.Security.Claims;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace ADSUS_BE.Middlewares;

/// <summary>
/// Kiểm tra tài khoản còn hiệu lực ngay sau khi chữ ký token được xác thực.
///
/// VÌ SAO CẦN: JWT là token tự chứng minh — backend không giữ danh sách token đã phát ra.
/// Nghĩa là Admin khoá một tài khoản (UC-04 / FT-08) nhưng token cấp trước đó VẪN gọi API
/// được cho tới khi hết hạn. Với hệ thống y tế thì không chấp nhận được.
///
/// Đặt ở tầng xác thực thay vì kiểm trong từng service, để mọi endpoint đang có VÀ mọi
/// endpoint các module sau viết thêm đều được bảo vệ mà không phải nhớ làm gì cả.
///
/// Hai luật trong tài liệu được thoả nhờ chỗ này:
///   - UC-02 AF-02: quét vân tay đúng nhưng tài khoản đã bị khoá/vô hiệu hoá -> vẫn từ chối.
///   - UC-04 FT-08: khoá tài khoản có hiệu lực ngay, không phải chờ token hết hạn.
///
/// ĐÁNH ĐỔI: mỗi request có token phải đọc DB thêm một lần. Với quy mô một phòng khám thì
/// không đáng kể. Nếu sau này thành nút thắt, hướng xử lý là cache trạng thái tài khoản
/// trong bộ nhớ và xoá cache khi Admin đổi trạng thái — KHÔNG phải bỏ hẳn kiểm tra này.
/// </summary>
public class AccountStatusJwtEvents : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var rawUserId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(rawUserId, out var userId))
        {
            // Chữ ký hợp lệ nhưng thiếu claim định danh — token không dùng được.
            context.Fail("Invalid access token.");
            return;
        }

        var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();

        // Chỉ đọc để kiểm tra trạng thái, không sửa/lưu gì ở đây — dùng bản AsNoTracking. Chạy
        // trên MỌI request có token nên đáng để tránh tracking không cần thiết (P11 review
        // Module 1, 14/08/2026).
        var user = await users.GetByIdReadOnlyAsync(userId, context.HttpContext.RequestAborted);

        // GB-06: không phân biệt tài khoản không tồn tại, bị khoá hay bị vô hiệu hoá —
        // mọi trường hợp đều trả 401 giống hệt nhau.
        if (user is null || user.Status != UserStatus.Active)
        {
            context.Fail("Invalid access token.");
        }
    }
}
