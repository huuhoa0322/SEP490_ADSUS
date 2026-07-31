/// Vai trò tài khoản. Khớp với enum user_role trong database.
enum UserRole { admin, doctor, nurse, patient, unknown }

UserRole userRoleFromApi(String? value) => switch (value?.toUpperCase()) {
      'ADMIN' => UserRole.admin,
      'DOCTOR' => UserRole.doctor,
      'NURSE' => UserRole.nurse,
      'PATIENT' => UserRole.patient,
      _ => UserRole.unknown,
    };

/// Phiên đăng nhập sau khi xác thực thành công (UC-01).
class AuthSession {
  const AuthSession({
    required this.accessToken,
    required this.fullName,
    required this.role,
    required this.mustChangePassword,
    this.email,
  });

  final String accessToken;
  final String fullName;
  final UserRole role;
  final String? email;

  /// TRUE thì phải ép người dùng đổi mật khẩu trước khi vào bất kỳ màn nào khác (UC-25).
  final bool mustChangePassword;
}
