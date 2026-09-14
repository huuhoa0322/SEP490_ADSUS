import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../domain/entities/auth_session.dart';

/// Bệnh nhân tự đăng ký (2 bước còn lại sau khi đổi sang Firebase) — xem plan gốc.
class RegisterState {
  const RegisterState({
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

  RegisterState copyWith({
    bool? isSubmitting,
    String? errorMessage,
    String? phoneNumber,
    String? verificationId,
    String? firebaseIdToken,
    AuthSession? completedSession,
    bool clearError = false,
    bool resetVerification = false,
  }) {
    return RegisterState(
      isSubmitting: isSubmitting ?? this.isSubmitting,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
      phoneNumber: phoneNumber ?? this.phoneNumber,
      verificationId: resetVerification ? null : (verificationId ?? this.verificationId),
      firebaseIdToken: resetVerification ? null : (firebaseIdToken ?? this.firebaseIdToken),
      completedSession: completedSession ?? this.completedSession,
    );
  }
}

class RegisterViewModel extends StateNotifier<RegisterState> {
  RegisterViewModel(this._ref) : super(const RegisterState());

  final Ref _ref;

  Future<bool> requestOtp(String phoneNumber) async {
    state = state.copyWith(
      isSubmitting: true,
      clearError: true,
      phoneNumber: phoneNumber,
      resetVerification: true,
    );

    try {
      final verificationId = await _ref
          .read(firebasePhoneAuthServiceProvider)
          .sendCode(localPhoneNumber: phoneNumber);
      state = state.copyWith(isSubmitting: false, verificationId: verificationId);
      return true;
    } on ApiException catch (e) {
      state = state.copyWith(isSubmitting: false, errorMessage: e.message);
      return false;
    }
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

  Future<bool> completeRegistration({
    required String fullName,
    required String password,
    required String confirmPassword,
    String? email,
    String? dateOfBirth,
  }) async {
    final phone = state.phoneNumber;
    final token = state.firebaseIdToken;
    if (phone == null || token == null) return false;

    state = state.copyWith(isSubmitting: true, clearError: true);
    try {
      final session = await _ref.read(authRepositoryProvider).completeRegistration(
            firebaseIdToken: token,
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
}

final registerViewModelProvider =
    StateNotifierProvider.autoDispose<RegisterViewModel, RegisterState>(
        (ref) => RegisterViewModel(ref));
