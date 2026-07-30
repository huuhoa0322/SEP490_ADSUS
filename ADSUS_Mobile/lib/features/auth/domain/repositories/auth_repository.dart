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
  Future<void> changePassword({
    required String currentPassword,
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
}
