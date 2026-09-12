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

      // Đổi sang tài khoản khác thì phải xoá trạng thái sinh trắc học của người trước.
      // Không có bước này, người sau sẽ thừa hưởng nút vân tay mà chính họ chưa hề bật.
      final pairedPhoneOnDevice = await _storage.read(key: StorageKeys.pairedPhone);
      if (pairedPhoneOnDevice != null && pairedPhoneOnDevice != phoneNumber) {
        await _storage.delete(key: StorageKeys.biometricEnabled);
      }

      await _storage.write(key: StorageKeys.accessToken, value: session.accessToken);

      // UC-02 BR-01: ghi lại rằng máy này đã đăng nhập bằng mật khẩu thành công.
      // Đây chính là bước "ghép đôi thiết bị" mà sinh trắc học yêu cầu.
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
  Future<void> changePassword({
    required String? currentPassword,
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
    // Xoá SẠCH cả ba khoá.
    //
    // Trước đây chỉ xoá token và cố ý giữ lại ghép đôi sinh trắc học, với ý định cho người
    // dùng đăng nhập lại bằng vân tay. Nhưng đăng nhập bằng vân tay lại cần chính token
    // vừa bị xoá, nên nút vân tay vẫn hiện mà bấm vào chỉ báo "phiên đã hết hạn".
    //
    // Giữ lại còn nguy hiểm hơn: máy dùng chung, người sau đăng nhập sẽ thấy nút vân tay
    // bật sẵn dù chưa từng bật.
    //
    // Sinh trắc học vì vậy chỉ phục vụ trường hợp thật sự cần: thoát app rồi mở lại mà
    // KHÔNG đăng xuất — token vẫn còn nên vân tay mở khoá được ngay. Đăng xuất là chủ động
    // kết thúc phiên, muốn dùng vân tay tiếp thì ghép đôi lại bằng mật khẩu.
    //
    // LỆCH TÀI LIỆU — cần nhóm chốt: UC-02 ngụ ý vân tay dùng được lâu dài sau một lần
    // đăng nhập mật khẩu. Muốn đúng như vậy thì backend phải có refresh token (hoặc token
    // thiết bị dài hạn) để vân tay đổi lấy phiên mới. Hiện backend chưa có.

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
    await _storage.delete(key: StorageKeys.biometricEnabled);
  }

  @override
  Future<String?> readStoredToken() => _storage.read(key: StorageKeys.accessToken);

  @override
  Future<bool> isBiometricPaired() async {
    final paired = await _storage.read(key: StorageKeys.pairedPhone);
    final enabled = await _storage.read(key: StorageKeys.biometricEnabled);
    final token = await _storage.read(key: StorageKeys.accessToken);

    // Phải thoả CẢ BA: đã đăng nhập bằng mật khẩu trên máy này (BR-01), người dùng đã chủ
    // động bật tính năng, VÀ còn token để mở khoá. Thiếu token mà vẫn hiện nút thì bấm vào
    // chỉ nhận được thông báo lỗi — thà đừng hiện.
    return paired != null &&
        paired.isNotEmpty &&
        enabled == 'true' &&
        token != null &&
        token.isNotEmpty;
  }

  @override
  Future<String?> readPairedPhone() =>
      _storage.read(key: StorageKeys.pairedPhone);

  @override
  Future<void> requestRegistrationOtp({required String phoneNumber}) async {
    try {
      await _dio.post<Map<String, dynamic>>(
        ApiConstants.registerRequestOtp,
        data: {'phoneNumber': phoneNumber},
      );
    } on DioException catch (e) {
      // Báo RÕ khi số đã có tài khoản (409) — quyết định có chủ đích, xem Global Constraints
      // ở plan gốc. Đặt riêng nhánh này TRƯỚC ApiErrorMapper.general vì message cụ thể ở đây
      // quan trọng hơn message chung "không gửi được".
      if (e.response?.statusCode == 409) {
        throw const ApiException('Số điện thoại này đã tồn tại.', statusCode: 409);
      }
      throw ApiErrorMapper.general(e, fallback: 'Không gửi được mã xác thực.');
    }
  }

  @override
  Future<String> verifyRegistrationOtp({
    required String phoneNumber,
    required String otpCode,
  }) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        ApiConstants.registerVerifyOtp,
        data: {'phoneNumber': phoneNumber, 'otpCode': otpCode},
      );

      final envelope = ApiEnvelope.fromJson(res.data ?? const {});
      if (envelope.data == null) {
        throw const ApiException('Mã xác thực không đúng hoặc đã hết hạn.');
      }

      return AuthMapper.registrationTokenFromJson(envelope.data!);
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e, fallback: 'Mã xác thực không đúng hoặc đã hết hạn.');
    }
  }

  @override
  Future<AuthSession> completeRegistration({
    required String registrationToken,
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
          'registrationToken': registrationToken,
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

      // Đăng ký xong tự động đăng nhập — ghi token giống hệt signIn (UC-02 BR-01: đây cũng
      // là bước "ghép đôi thiết bị" cho sinh trắc học), để mọi màn sau đó đối xử với tài
      // khoản mới này y như vừa đăng nhập thủ công.
      await _storage.write(key: StorageKeys.accessToken, value: session.accessToken);
      await _storage.write(key: StorageKeys.pairedPhone, value: phoneNumber);

      return session;
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e, fallback: 'Đăng ký thất bại.');
    }
  }

  @override
  Future<void> requestPasswordResetOtp({required String phoneNumber}) async {
    try {
      await _dio.post<Map<String, dynamic>>(
        ApiConstants.forgotPasswordRequestOtp,
        data: {'phoneNumber': phoneNumber},
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 404) {
        throw const ApiException('Số điện thoại này chưa có tài khoản.', statusCode: 404);
      }
      throw ApiErrorMapper.general(e, fallback: 'Không gửi được mã xác thực.');
    }
  }

  @override
  Future<String> verifyPasswordResetOtp({
    required String phoneNumber,
    required String otpCode,
  }) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        ApiConstants.forgotPasswordVerifyOtp,
        data: {'phoneNumber': phoneNumber, 'otpCode': otpCode},
      );

      final envelope = ApiEnvelope.fromJson(res.data ?? const {});
      if (envelope.data == null) {
        throw const ApiException('Mã xác thực không đúng hoặc đã hết hạn.');
      }

      return envelope.data!['resetToken'] as String? ?? '';
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e, fallback: 'Mã xác thực không đúng hoặc đã hết hạn.');
    }
  }

  @override
  Future<AuthSession> completePasswordResetWithOtp({
    required String resetToken,
    required String newPassword,
    required String confirmNewPassword,
    required String phoneNumber,
  }) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        ApiConstants.forgotPasswordComplete,
        data: {
          'resetToken': resetToken,
          'newPassword': newPassword,
          'confirmNewPassword': confirmNewPassword,
        },
      );

      final envelope = ApiEnvelope.fromJson(res.data ?? const {});
      if (envelope.data == null) {
        throw const ApiException('Đặt lại mật khẩu thất bại.');
      }

      final session = AuthMapper.sessionFromJson(envelope.data!);

      // Tự động đăng nhập lại sau khi đổi mật khẩu — ghi token giống hệt signIn/completeRegistration.
      await _storage.write(key: StorageKeys.accessToken, value: session.accessToken);
      await _storage.write(key: StorageKeys.pairedPhone, value: phoneNumber);

      return session;
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e, fallback: 'Đặt lại mật khẩu thất bại.');
    }
  }
}
