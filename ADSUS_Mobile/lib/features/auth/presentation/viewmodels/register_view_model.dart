import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../domain/entities/auth_session.dart';

/// Bệnh nhân tự đăng ký (3 bước) — xem plan gốc cho toàn bộ quyết định thiết kế.
///
/// Một ViewModel DUY NHẤT cho cả 3 màn (điện thoại → OTP → hoàn tất hồ sơ), để
/// [phoneNumber]/[registrationToken] không bị mất khi Navigator chuyển màn — mirror lý do
/// AuthViewModel giữ session xuyên suốt nhiều màn, không phải vì 3 bước này "giống" nhau.
class RegisterState {
  const RegisterState({
    this.isSubmitting = false,
    this.errorMessage,
    this.otpSent = false,
    this.otpVerified = false,
    this.phoneNumber,
    this.registrationToken,
    this.completedSession,
  });

  final bool isSubmitting;
  final String? errorMessage;
  final bool otpSent;
  final bool otpVerified;
  final String? phoneNumber;
  final String? registrationToken;
  final AuthSession? completedSession;

  RegisterState copyWith({
    bool? isSubmitting,
    String? errorMessage,
    bool? otpSent,
    bool? otpVerified,
    String? phoneNumber,
    String? registrationToken,
    AuthSession? completedSession,
    bool clearError = false,
  }) {
    return RegisterState(
      isSubmitting: isSubmitting ?? this.isSubmitting,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
      otpSent: otpSent ?? this.otpSent,
      otpVerified: otpVerified ?? this.otpVerified,
      phoneNumber: phoneNumber ?? this.phoneNumber,
      registrationToken: registrationToken ?? this.registrationToken,
      completedSession: completedSession ?? this.completedSession,
    );
  }
}

class RegisterViewModel extends StateNotifier<RegisterState> {
  RegisterViewModel(this._ref) : super(const RegisterState());

  final Ref _ref;

  Future<bool> requestOtp(String phoneNumber) async {
    state = state.copyWith(isSubmitting: true, clearError: true);
    try {
      await _ref.read(authRepositoryProvider).requestRegistrationOtp(phoneNumber: phoneNumber);
      state = state.copyWith(isSubmitting: false, otpSent: true, phoneNumber: phoneNumber);
      return true;
    } on ApiException catch (e) {
      state = state.copyWith(isSubmitting: false, errorMessage: e.message);
      return false;
    }
  }

  Future<bool> verifyOtp(String otpCode) async {
    final phone = state.phoneNumber;
    if (phone == null) return false; // Không thể tới màn OTP mà chưa qua bước xin mã.

    state = state.copyWith(isSubmitting: true, clearError: true);
    try {
      final token = await _ref
          .read(authRepositoryProvider)
          .verifyRegistrationOtp(phoneNumber: phone, otpCode: otpCode);
      state = state.copyWith(
        isSubmitting: false, otpVerified: true, registrationToken: token);
      return true;
    } on ApiException catch (e) {
      state = state.copyWith(isSubmitting: false, errorMessage: e.message);
      return false;
    }
  }

  Future<bool> completeRegistration({
    required String fullName,
    required String password,
    required String confirmPassword,
    String? email,
    String? dateOfBirth,
  }) async {
    final phone = state.phoneNumber;
    final token = state.registrationToken;
    if (phone == null || token == null) return false; // Chưa qua đủ 2 bước trước.

    state = state.copyWith(isSubmitting: true, clearError: true);
    try {
      final session = await _ref.read(authRepositoryProvider).completeRegistration(
            registrationToken: token,
            fullName: fullName,
            password: password,
            confirmPassword: confirmPassword,
            phoneNumber: phone,
            email: email,
            dateOfBirth: dateOfBirth,
          );
      state = state.copyWith(isSubmitting: false, completedSession: session);
      return true;
    } on ApiException catch (e) {
      state = state.copyWith(isSubmitting: false, errorMessage: e.message);
      return false;
    }
  }

  void clearError() => state = state.copyWith(clearError: true);
}

final registerViewModelProvider =
    StateNotifierProvider.autoDispose<RegisterViewModel, RegisterState>(
        (ref) => RegisterViewModel(ref));
