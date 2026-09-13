import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../domain/entities/auth_session.dart';

/// UC-03 — quên mật khẩu qua Firebase Phone Auth, CHỈ dành cho Patient.
class ForgotPasswordOtpState {
  const ForgotPasswordOtpState({
    this.isSubmitting = false,
    this.errorMessage,
    this.phoneNumber,
    this.verificationId,
    this.firebaseIdToken,
    this.completedSession,
  });

  final bool isSubmitting;
  final String? errorMessage;
  final String? phoneNumber;
  final String? verificationId;
  final String? firebaseIdToken;
  final AuthSession? completedSession;

  ForgotPasswordOtpState copyWith({
    bool? isSubmitting,
    String? errorMessage,
    String? phoneNumber,
    String? verificationId,
    String? firebaseIdToken,
    AuthSession? completedSession,
    bool clearError = false,
  }) {
    return ForgotPasswordOtpState(
      isSubmitting: isSubmitting ?? this.isSubmitting,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
      phoneNumber: phoneNumber ?? this.phoneNumber,
      verificationId: verificationId ?? this.verificationId,
      firebaseIdToken: firebaseIdToken ?? this.firebaseIdToken,
      completedSession: completedSession ?? this.completedSession,
    );
  }
}

class ForgotPasswordOtpViewModel extends StateNotifier<ForgotPasswordOtpState> {
  ForgotPasswordOtpViewModel(this._ref) : super(const ForgotPasswordOtpState());

  final Ref _ref;

  Future<bool> requestOtp(String phoneNumber) async {
    state = state.copyWith(isSubmitting: true, clearError: true, phoneNumber: phoneNumber);
    var succeeded = false;

    await _ref.read(firebasePhoneAuthServiceProvider).sendCode(
      localPhoneNumber: phoneNumber,
      onCodeSent: (verificationId) {
        state = state.copyWith(isSubmitting: false, verificationId: verificationId);
        succeeded = true;
      },
      onFailed: (message) {
        state = state.copyWith(isSubmitting: false, errorMessage: message);
      },
    );

    return succeeded;
  }

  Future<bool> verifyOtp(String otpCode) async {
    final verificationId = state.verificationId;
    if (verificationId == null) return false;

    state = state.copyWith(isSubmitting: true, clearError: true);
    try {
      final token = await _ref.read(firebasePhoneAuthServiceProvider).confirmCode(
            verificationId: verificationId,
            smsCode: otpCode,
          );
      state = state.copyWith(isSubmitting: false, firebaseIdToken: token);
      return true;
    } catch (_) {
      state = state.copyWith(
        isSubmitting: false, errorMessage: 'Mã xác thực không đúng hoặc đã hết hạn.');
      return false;
    }
  }

  Future<bool> completeReset({
    required String newPassword,
    required String confirmNewPassword,
  }) async {
    final phone = state.phoneNumber;
    final token = state.firebaseIdToken;
    if (phone == null || token == null) return false;

    state = state.copyWith(isSubmitting: true, clearError: true);
    try {
      final session = await _ref.read(authRepositoryProvider).completePasswordResetWithFirebase(
            firebaseIdToken: token,
            newPassword: newPassword,
            confirmNewPassword: confirmNewPassword,
            phoneNumber: phone,
          );
      state = state.copyWith(isSubmitting: false, completedSession: session);
      return true;
    } on ApiException catch (e) {
      state = state.copyWith(isSubmitting: false, errorMessage: e.message);
      return false;
    }
  }
}

final forgotPasswordOtpViewModelProvider =
    StateNotifierProvider.autoDispose<ForgotPasswordOtpViewModel, ForgotPasswordOtpState>(
        (ref) => ForgotPasswordOtpViewModel(ref));
