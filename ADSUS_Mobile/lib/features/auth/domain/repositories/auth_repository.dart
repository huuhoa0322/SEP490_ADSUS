import '../entities/auth_session.dart';
import '../entities/user_profile.dart';

/// Hợp đồng cho tầng dữ liệu xác thực.
///
/// Tách interface ra để viewmodel test được bằng bản giả, không cần gọi API thật.
abstract interface class AuthRepository {
  /// UC-01 — đăng nhập bằng số điện thoại và mật khẩu.
  /// Ném ApiException với đúng một câu chung cho mọi trường hợp thất bại (GB-06).
  Future<AuthSession> signIn({
    required String phoneNumber,
    required String password,
  });

  /// UC-25 — đổi mật khẩu của chính mình.
  /// UC-03 FT-06 — yêu cầu cấp lại mật khẩu.
  ///
  /// Trả về void CÓ CHỦ Ý: backend luôn trả cùng một câu dù thông tin đúng hay sai (AF-01),
  /// nên ở đây cũng không có gì để phân biệt. Đừng đổi thành Future&lt;bool&gt; — đó chính là
  /// lỗ hổng dò xem số điện thoại nào đã có tài khoản.
  Future<void> requestPasswordReset({
    required String phoneNumber,
    required String email,
  });

  /// currentPassword bỏ trống được (sửa 06/08/2026) khi tài khoản còn đang dùng mật khẩu tạm
  /// (mustChangePassword) — backend tự bỏ qua bước xác thực trong trường hợp đó, dựa trên cờ
  /// phía server chứ không phải giá trị client gửi lên.
  ///
  /// Trả về [AuthSession] MỚI (sửa 22/09/2026) — chứ không phải void như trước: token cũ vẫn
  /// còn mang claim MustChangePassword, và MustChangePasswordMiddleware phía backend tiếp tục
  /// từ chối MỌI request khác dùng token đó, kể cả sau khi đổi mật khẩu thành công. Backend đã
  /// trả sẵn 1 token mới trong response (đúng dữ liệu như lúc đăng nhập) chính vì lý do này —
  /// bản cũ của hàm này bỏ qua luôn phần dữ liệu đó, nên trên Mobile 1 bệnh nhân vừa bị ép đổi
  /// mật khẩu lần đầu sẽ tiếp tục dính 403 "Bạn phải đổi mật khẩu..." ở mọi màn hình sau đó
  /// (thông báo, đồng bộ widget...) cho tới khi tự đăng xuất rồi đăng nhập lại. Việc trả về
  /// AuthSession đầy đủ ở đây không vi phạm AF-01/GB-06 — hàm vẫn chỉ có đúng 1 đường thành
  /// công (ném ApiException cho mọi thất bại), không hề lộ thêm thông tin phân biệt được.
  Future<AuthSession> changePassword({
    required String? currentPassword,
    required String newPassword,
    required String confirmNewPassword,
  });

  /// UC-10 — lấy hồ sơ cá nhân.
  Future<UserProfile> getMyProfile();

  /// UC-10 — cập nhật hồ sơ. Số điện thoại không nằm trong tham số nên không đổi được (BR-02).
  Future<void> updateMyProfile({
    required String fullName,
    String? email,
    String? dateOfBirth,
  });

  /// Kết thúc phiên: xoá token và mọi dấu vết phiên trên máy.
  Future<void> signOut();

  /// Số điện thoại đang đăng nhập trên máy này, hoặc null nếu chưa đăng nhập lần nào —
  /// dùng để phân biệt cache dữ liệu cục bộ (ví dụ reminder preferences) giữa các tài khoản.
  Future<String?> readPairedPhone();

  /// Tự đăng ký — bước duy nhất còn lại sau khi Mobile đã xác thực số điện thoại qua Firebase
  /// (xem FirebasePhoneAuthService). Nhận `firebaseIdToken` thay vì registrationToken tự sinh.
  Future<AuthSession> completeRegistration({
    required String firebaseIdToken,
    required String fullName,
    required String password,
    required String confirmPassword,
    required String phoneNumber,
    String? email,
    String? dateOfBirth,
  });

  /// Quên mật khẩu qua Firebase — bước duy nhất. Ném ApiException(statusCode: 404) nếu số
  /// chưa có tài khoản Patient Active.
  Future<AuthSession> completePasswordResetWithFirebase({
    required String firebaseIdToken,
    required String newPassword,
    required String confirmNewPassword,
    required String phoneNumber,
  });
}
