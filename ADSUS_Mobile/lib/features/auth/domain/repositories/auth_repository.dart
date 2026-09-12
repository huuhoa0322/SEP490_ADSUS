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
  Future<void> changePassword({
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

  /// UC-02 — bật/tắt đăng nhập sinh trắc học ở phía máy chủ.
  Future<void> setBiometricEnabled(bool enabled);

  /// Kết thúc phiên: xoá token và mọi dấu vết phiên trên máy.
  Future<void> signOut();

  /// Token đã lưu, hoặc null nếu chưa đăng nhập lần nào.
  Future<String?> readStoredToken();

  /// UC-02 BR-01 — máy này đã từng đăng nhập bằng mật khẩu thành công chưa,
  /// và người dùng có bật sinh trắc học không.
  Future<bool> isBiometricPaired();

  /// Số điện thoại đã ghép đôi, hoặc null nếu chưa đăng nhập lần nào.
  Future<String?> readPairedPhone();

  /// Tự đăng ký bước 1 — xin gửi mã OTP tới số điện thoại.
  ///
  /// Ném ApiException(statusCode: 409, message: 'Số điện thoại này đã tồn tại.') nếu số đã
  /// có tài khoản — backend báo RÕ trường hợp này (quyết định có chủ đích, KHÔNG mirror
  /// AF-01 của requestPasswordReset — xem Global Constraints ở plan gốc). Cũng ném lỗi nếu
  /// sai định dạng số hoặc xin lại quá sớm (<60s).
  Future<void> requestRegistrationOtp({required String phoneNumber});

  /// Tự đăng ký bước 2 — xác thực mã OTP, đổi lấy registration token dùng cho bước 3.
  Future<String> verifyRegistrationOtp({
    required String phoneNumber,
    required String otpCode,
  });

  /// Tự đăng ký bước 3 (cuối) — tạo tài khoản Patient và tự động đăng nhập.
  /// Trả về AuthSession giống signIn — gọi nơi dùng chỉ cần lưu token y hệt luồng đăng nhập.
  Future<AuthSession> completeRegistration({
    required String registrationToken,
    required String fullName,
    required String password,
    required String confirmPassword,
    required String phoneNumber,
    String? email,
    String? dateOfBirth,
  });

  /// UC-03 — quên mật khẩu qua SMS OTP, bước 1 (thêm 12/09/2026). CHỈ dành cho tài khoản
  /// Patient — khác [requestPasswordReset] (email) đã có, KHÔNG thay thế nó.
  ///
  /// Ném ApiException(statusCode: 404) nếu số chưa có tài khoản Patient Active — báo RÕ,
  /// nhất quán với quyết định đã đổi ở tự đăng ký (KHÔNG mirror AF-01 của requestPasswordReset).
  Future<void> requestPasswordResetOtp({required String phoneNumber});

  /// Bước 2 — xác thực mã, đổi lấy reset token dùng cho bước 3.
  Future<String> verifyPasswordResetOtp({
    required String phoneNumber,
    required String otpCode,
  });

  /// Bước 3 (cuối) — đặt mật khẩu mới và tự động đăng nhập lại.
  Future<AuthSession> completePasswordResetWithOtp({
    required String resetToken,
    required String newPassword,
    required String confirmNewPassword,
    required String phoneNumber,
  });
}
