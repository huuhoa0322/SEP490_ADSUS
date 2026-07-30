import 'auth_session.dart';

/// Hồ sơ cá nhân hiển thị trên SCR-03 (UC-10).
class UserProfile {
  const UserProfile({
    required this.fullName,
    required this.phoneNumber,
    required this.role,
    required this.biometricEnabled,
    this.email,
    this.dateOfBirth,
  });

  final String fullName;

  /// Chỉ đọc — BR-02: số điện thoại là định danh đăng nhập, muốn đổi phải liên hệ phòng khám.
  final String phoneNumber;

  final String? email;

  /// Định dạng yyyy-MM-dd, null nếu chưa khai.
  final String? dateOfBirth;

  final UserRole role;
  final bool biometricEnabled;

  UserProfile copyWith({
    String? fullName,
    String? email,
    String? dateOfBirth,
    bool? biometricEnabled,
    bool clearEmail = false,
    bool clearDateOfBirth = false,
  }) {
    return UserProfile(
      fullName: fullName ?? this.fullName,
      phoneNumber: phoneNumber,
      email: clearEmail ? null : (email ?? this.email),
      dateOfBirth: clearDateOfBirth ? null : (dateOfBirth ?? this.dateOfBirth),
      role: role,
      biometricEnabled: biometricEnabled ?? this.biometricEnabled,
    );
  }
}
