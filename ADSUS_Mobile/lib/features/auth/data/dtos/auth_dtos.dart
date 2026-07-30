import '../../domain/entities/auth_session.dart';
import '../../domain/entities/user_profile.dart';

/// Vỏ bọc chung của mọi response từ ADSUS_BE — { code, message, data }.
class ApiEnvelope {
  const ApiEnvelope({required this.code, required this.message, this.data});

  final int code;
  final String message;
  final Map<String, dynamic>? data;

  factory ApiEnvelope.fromJson(Map<String, dynamic> json) => ApiEnvelope(
        code: json['code'] as int? ?? 0,
        message: json['message'] as String? ?? '',
        data: json['data'] as Map<String, dynamic>?,
      );
}

/// Chuyển JSON thành entity của tầng domain.
class AuthMapper {
  const AuthMapper._();

  static AuthSession sessionFromJson(Map<String, dynamic> json) => AuthSession(
        accessToken: json['accessToken'] as String? ?? '',
        fullName: json['fullName'] as String? ?? '',
        email: json['email'] as String?,
        role: userRoleFromApi(json['role'] as String?),
        mustChangePassword: json['mustChangePassword'] as bool? ?? false,
      );

  static UserProfile profileFromJson(Map<String, dynamic> json) => UserProfile(
        fullName: json['fullName'] as String? ?? '',
        phoneNumber: json['phoneNumber'] as String? ?? '',
        email: json['email'] as String?,
        dateOfBirth: json['dateOfBirth'] as String?,
        role: userRoleFromApi(json['role'] as String?),
        biometricEnabled: json['biometricEnabled'] as bool? ?? false,
        mustChangePassword: json['mustChangePassword'] as bool? ?? false,
      );
}
