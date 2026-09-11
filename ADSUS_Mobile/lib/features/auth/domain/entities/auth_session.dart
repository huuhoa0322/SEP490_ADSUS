import 'dart:convert';

/// Vai trò tài khoản. Khớp với enum user_role trong database.
enum UserRole { admin, doctor, staff, patient, unknown }

UserRole userRoleFromApi(String? value) => switch (value?.toUpperCase()) {
      'ADMIN' => UserRole.admin,
      'DOCTOR' => UserRole.doctor,
      'STAFF' => UserRole.staff,
      'PATIENT' => UserRole.patient,
      _ => UserRole.unknown,
    };

/// Phiên đăng nhập sau khi xác thực thành công (UC-01).
class AuthSession {
  const AuthSession({
    required this.userId,
    required this.accessToken,
    required this.fullName,
    required this.role,
    required this.mustChangePassword,
    this.email,
  });

  /// Subject claim từ JWT — dùng làm Hive box key.
  final String userId;

  final String accessToken;
  final String fullName;
  final UserRole role;
  final String? email;

  /// TRUE thì phải ép người dùng đổi mật khẩu trước khi vào bất kỳ màn nào khác (UC-25).
  final bool mustChangePassword;

  /// Trích `sub` claim từ JWT để lấy userId.
  /// JWT payload = base64url(JSON). Trảng '' nếu parse thất bại.
  static String extractUserId(String token) {
    try {
      final parts = token.split('.');
      if (parts.length < 2) return '';
      // base64url → base64: thay đổi ký tự đặc biệt, thêm padding.
      final payload = parts[1]
          .replaceAll('-', '+')
          .replaceAll('_', '/')
          .padRight(parts[1].length + (4 - parts[1].length % 4) % 4, '=');
      final decoded = utf8.decode(base64Decode(payload));
      final json = jsonDecode(decoded) as Map<String, dynamic>;
      return json['sub'] as String? ?? '';
    } catch (_) {
      return '';
    }
  }
}
