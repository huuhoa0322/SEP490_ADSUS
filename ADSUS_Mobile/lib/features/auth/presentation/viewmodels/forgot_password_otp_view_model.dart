import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../domain/entities/auth_session.dart';

/// UC-03 — quên mật khẩu qua SMS OTP (thêm 12/09/2026), CHỈ dành cho Patient. Song song với
/// [ForgotPasswordViewModel] (email) đã có — không thay thế, không đụng file đó.
class ForgotPasswordOtpState {
  const ForgotPasswordOtpState({
    this.isSubmitting = false,
    this.errorMessage,
    this.otpVerified = false,
    this.phoneNumber,
    this.resetToken,
    this.completedSession,
  });

  final bool isSubmitting;
  final String? errorMessage;
  final bool otpVerified;
  final String? phoneNumber;
  final String? resetToken;
  final AuthSession? completedSession;

  ForgotPasswordOtpState copyWith({
    bool? isSubmitting,
    String? errorMessage,
    bool? otpVerified,
    String? phoneNumber,
    String? resetToken,
    AuthSession? completedSession,
    bool clearError = false,
  }) {
    return ForgotPasswordOtpState(
      isSubmitting: isSubmitting ?? this.isSubmitting,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
      otpVerified: otpVerified ?? this.otpVerified,
      phoneNumber: phoneNumber ?? this.phoneNumber,
      resetToken: resetToken ?? this.resetToken,
      completedSession: completedSession ?? this.completedSession,
    );
  }
}

class ForgotPasswordOtpViewModel extends StateNotifier<ForgotPasswordOtpState> {
  ForgotPasswordOtpViewModel(this._ref) : super(const ForgotPasswordOtpState());

  final Ref _ref;

  Future<bool> requestOtp(String phoneNumber) async {
    state = state.copyWith(isSubmitting: true, clearError: true);
    try {
      await _ref.read(authRepositoryProvider).requestPasswordResetOtp(phoneNumber: phoneNumber);
      state = state.copyWith(isSubmitting: false, phoneNumber: phoneNumber);
      return true;
    } on ApiException catch (e) {
      state = state.copyWith(isSubmitting: false, errorMessage: e.message);
      return false;
    }
  }

  Future<bool> verifyOtp(String otpCode) async {
    final phone = state.phoneNumber;
    if (phone == null) return false;

    state = state.copyWith(isSubmitting: true, clearError: true);
    try {
      final token = await _ref
          .read(authRepositoryProvider)
          .verifyPasswordResetOtp(phoneNumber: phone, otpCode: otpCode);
      state = state.copyWith(isSubmitting: false, otpVerified: true, resetToken: token);
      return true;
    } on ApiException catch (e) {
      state = state.copyWith(isSubmitting: false, errorMessage: e.message);
      return false;
    }
  }

  Future<bool> completeReset({
    required String newPassword,
    required String confirmNewPassword,
  }) async {
    final phone = state.phoneNumber;
    final token = state.resetToken;
    if (phone == null || token == null) return false;

    state = state.copyWith(isSubmitting: true, clearError: true);
    try {
      final session = await _ref.read(authRepositoryProvider).completePasswordResetWithOtp(
            resetToken: token,
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

  void clearError() => state = state.copyWith(clearError: true);
}

final forgotPasswordOtpViewModelProvider =
    StateNotifierProvider.autoDispose<ForgotPasswordOtpViewModel, ForgotPasswordOtpState>(
        (ref) => ForgotPasswordOtpViewModel(ref));
