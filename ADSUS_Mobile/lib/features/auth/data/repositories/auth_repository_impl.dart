import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../../../../core/constants/api_constants.dart';
import '../../../../core/constants/storage_keys.dart';
import '../../../../core/network/api_exception.dart';
import '../../../../features/notification/services/notification_service.dart';
import '../../domain/entities/auth_session.dart';
import '../../domain/entities/user_profile.dart';
import '../../domain/repositories/auth_repository.dart';
import '../dtos/auth_dtos.dart';
import 'auth_error_mapper.dart';

/// Vai trò duy nhất được dùng ứng dụng di động.
///
/// UC-01: SCR-02 (Mobile) dành cho Bệnh nhân; Admin, Bác sĩ và Điều dưỡng đăng nhập trên
/// Web qua SCR-01. Bảng quyền PRD §3.2 cũng không giao chức năng nào của ba vai trò kia
/// cho ứng dụng di động.
const UserRole mobileAllowedRole = UserRole.patient;

class AuthRepositoryImpl implements AuthRepository {
  // Dùng tham số vị trí thay vì tham số có tên, vì Dart không cho phép tên tham số bắt
  // đầu bằng dấu gạch dưới. Hai tham số khác kiểu nhau nên không sợ truyền nhầm thứ tự.
  AuthRepositoryImpl(this._dio, this._storage);

  final Dio _dio;
  final FlutterSecureStorage _storage;

  @override
  Future<AuthSession> signIn({
    required String phoneNumber,
    required String password,
  }) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        ApiConstants.login,
        data: {'phoneNumber': phoneNumber, 'password': password},
      );

      final envelope = ApiEnvelope.fromJson(res.data ?? const {});
      if (envelope.data == null) {
        throw const ApiException(AuthErrorMapper.signInFailed);
      }

      final session = AuthMapper.sessionFromJson(envelope.data!);

      // Chặn TRƯỚC khi ghi bất cứ thứ gì xuống máy. Nếu ghi rồi mới chặn thì token của
      // bác sĩ vẫn nằm lại trong thiết bị dù họ không vào được ứng dụng.
      if (session.role != mobileAllowedRole) {
        throw const ApiException(
          'Tài khoản này sử dụng giao diện web của ADSUS. '
          'Ứng dụng di động chỉ dành cho bệnh nhân.',
        );
      }

      await _storage.write(key: StorageKeys.accessToken, value: session.accessToken);

      // Ghi lại số điện thoại đang đăng nhập trên máy này — dùng để phân biệt cache
      // reminder preferences giữa các tài khoản (xem reminder_preference_store.dart).
      await _storage.write(key: StorageKeys.pairedPhone, value: phoneNumber);

      // Register FCM token to backend for push notifications.
      // Best-effort: failure should not block login.
      try {
        await notificationService.registerTokenWithBackend(session.accessToken);
      } catch (_) {
        // Ignore - user can still use app without push notifications
      }

      return session;
    } on DioException catch (e) {
      throw AuthErrorMapper.forSignIn(e);
    }
  }

  @override
  Future<void> requestPasswordReset({
    required String phoneNumber,
    required String email,
  }) async {
    try {
      await _dio.post<Map<String, dynamic>>(
        ApiConstants.forgotPassword,
        data: {'phoneNumber': phoneNumber, 'email': email},
      );
    } on DioException catch (e) {
      // Chỉ 400 (sai định dạng) và lỗi mạng mới tới được đây. Backend không bao giờ trả lỗi
      // vì "không tìm thấy tài khoản" — đó là chủ đích của AF-01.
      throw ApiErrorMapper.general(e, fallback: 'Không gửi được yêu cầu.');
    }
  }

  @override
  Future<AuthSession> changePassword({
    required String? currentPassword,
    required String newPassword,
    required String confirmNewPassword,
  }) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        ApiConstants.changePassword,
        data: {
          'currentPassword': currentPassword,
          'newPassword': newPassword,
          'confirmNewPassword': confirmNewPassword,
        },
      );

      final envelope = ApiEnvelope.fromJson(res.data ?? const {});
      if (envelope.data == null) {
        throw const ApiException('Đổi mật khẩu thất bại.');
      }

      final session = AuthMapper.sessionFromJson(envelope.data!);

      // Token cũ (còn mang claim MustChangePassword) phải bị thay ngay — không có bước này,
      // mọi request kế tiếp vẫn tự gắn token cũ (xem dio_client.dart, đọc từ chính key này)
      // và tiếp tục bị MustChangePasswordMiddleware từ chối dù đổi mật khẩu đã thành công.
      await _storage.write(key: StorageKeys.accessToken, value: session.accessToken);

      return session;
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e, fallback: 'Đổi mật khẩu thất bại.');
    }
  }

  @override
  Future<UserProfile> getMyProfile() async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(ApiConstants.myProfile);
      final envelope = ApiEnvelope.fromJson(res.data ?? const {});
      if (envelope.data == null) {
        throw const ApiException('Không tải được hồ sơ cá nhân.');
      }
      return AuthMapper.profileFromJson(envelope.data!);
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e, fallback: 'Không tải được hồ sơ cá nhân.');
    }
  }

  @override
  Future<void> updateMyProfile({
    required String fullName,
    String? email,
    String? dateOfBirth,
  }) async {
    try {
      await _dio.put<Map<String, dynamic>>(
        ApiConstants.myProfile,
        // Không gửi phoneNumber — BR-02, số điện thoại không đổi được từ đây.
        data: {
          'fullName': fullName,
          'email': email,
          'dateOfBirth': dateOfBirth,
        },
      );
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e, fallback: 'Cập nhật hồ sơ thất bại.');
    }
  }

  @override
  Future<void> signOut() async {
    // Unregister FCM token before deleting the access token.
    final token = await _storage.read(key: StorageKeys.accessToken);
    if (token != null) {
      try {
        await notificationService.unregisterTokenFromBackend(token);
      } catch (_) {
        // Ignore - proceed with logout even if unregister fails
      }
    }

    await _storage.delete(key: StorageKeys.accessToken);
    await _storage.delete(key: StorageKeys.pairedPhone);
  }

  @override
  Future<String?> readPairedPhone() =>
      _storage.read(key: StorageKeys.pairedPhone);

  @override
  Future<AuthSession> completeRegistration({
    required String firebaseIdToken,
    required String fullName,
    required String password,
    required String confirmPassword,
    required String phoneNumber,
    String? email,
    String? dateOfBirth,
  }) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        ApiConstants.registerComplete,
        data: {
          'firebaseIdToken': firebaseIdToken,
          'fullName': fullName,
          'password': password,
          'confirmPassword': confirmPassword,
          'email': email,
          'dateOfBirth': dateOfBirth,
        },
      );

      final envelope = ApiEnvelope.fromJson(res.data ?? const {});
      if (envelope.data == null) {
        throw const ApiException('Đăng ký thất bại.');
      }

      final session = AuthMapper.sessionFromJson(envelope.data!);

      await _storage.write(key: StorageKeys.accessToken, value: session.accessToken);
      await _storage.write(key: StorageKeys.pairedPhone, value: phoneNumber);

      return session;
    } on DioException catch (e) {
      if (e.response?.statusCode == 409) {
        throw const ApiException('Số điện thoại này đã tồn tại.', statusCode: 409);
      }
      throw ApiErrorMapper.general(e, fallback: 'Đăng ký thất bại.');
    }
  }

  @override
  Future<AuthSession> completePasswordResetWithFirebase({
    required String firebaseIdToken,
    required String newPassword,
    required String confirmNewPassword,
    required String phoneNumber,
  }) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        ApiConstants.forgotPasswordCompleteWithFirebase,
        data: {
          'firebaseIdToken': firebaseIdToken,
          'newPassword': newPassword,
          'confirmNewPassword': confirmNewPassword,
        },
      );

      final envelope = ApiEnvelope.fromJson(res.data ?? const {});
      if (envelope.data == null) {
        throw const ApiException('Đặt lại mật khẩu thất bại.');
      }

      final session = AuthMapper.sessionFromJson(envelope.data!);

      await _storage.write(key: StorageKeys.accessToken, value: session.accessToken);
      await _storage.write(key: StorageKeys.pairedPhone, value: phoneNumber);

      return session;
    } on DioException catch (e) {
      if (e.response?.statusCode == 404) {
        throw const ApiException('Số điện thoại này chưa có tài khoản.', statusCode: 404);
      }
      throw ApiErrorMapper.general(e, fallback: 'Đặt lại mật khẩu thất bại.');
    }
  }
}
