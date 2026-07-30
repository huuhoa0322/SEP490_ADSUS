import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../../../../core/constants/api_constants.dart';
import '../../../../core/constants/storage_keys.dart';
import '../../../../core/network/api_exception.dart';
import '../../domain/entities/auth_session.dart';
import '../../domain/entities/user_profile.dart';
import '../../domain/repositories/auth_repository.dart';
import '../dtos/auth_dtos.dart';

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
        throw const ApiException(ApiErrorMapper.signInFailed);
      }

      final session = AuthMapper.sessionFromJson(envelope.data!);

      await _storage.write(key: StorageKeys.accessToken, value: session.accessToken);

      // UC-02 BR-01: ghi lại rằng máy này đã đăng nhập bằng mật khẩu thành công.
      // Đây chính là bước "ghép đôi thiết bị" mà sinh trắc học yêu cầu.
      await _storage.write(key: StorageKeys.pairedPhone, value: phoneNumber);

      return session;
    } on DioException catch (e) {
      throw ApiErrorMapper.forSignIn(e);
    }
  }

  @override
  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
    required String confirmNewPassword,
  }) async {
    try {
      await _dio.post<Map<String, dynamic>>(
        ApiConstants.changePassword,
        data: {
          'currentPassword': currentPassword,
          'newPassword': newPassword,
          'confirmNewPassword': confirmNewPassword,
        },
      );
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
  Future<void> setBiometricEnabled(bool enabled) async {
    try {
      await _dio.put<Map<String, dynamic>>(
        ApiConstants.myBiometric,
        data: {'enabled': enabled},
      );
      await _storage.write(
        key: StorageKeys.biometricEnabled,
        value: enabled.toString(),
      );
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e, fallback: 'Không đổi được cài đặt sinh trắc học.');
    }
  }

  @override
  Future<void> signOut() async {
    await _storage.delete(key: StorageKeys.accessToken);
    // Cố ý GIỮ LẠI pairedPhone và biometricEnabled: đăng xuất không có nghĩa là huỷ ghép
    // đôi thiết bị. Lần sau người dùng vẫn dùng được vân tay mà không phải nhập lại mật khẩu.
  }

  @override
  Future<String?> readStoredToken() => _storage.read(key: StorageKeys.accessToken);

  @override
  Future<bool> isBiometricPaired() async {
    final paired = await _storage.read(key: StorageKeys.pairedPhone);
    final enabled = await _storage.read(key: StorageKeys.biometricEnabled);
    // Phải thoả CẢ HAI: đã từng đăng nhập bằng mật khẩu trên máy này (BR-01),
    // VÀ người dùng đã chủ động bật tính năng.
    return paired != null && paired.isNotEmpty && enabled == 'true';
  }
}
