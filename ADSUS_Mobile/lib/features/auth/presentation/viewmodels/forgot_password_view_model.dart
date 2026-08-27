import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';

/// UC-03 FT-06 — người dùng tự yêu cầu cấp lại mật khẩu.
///
/// Thêm 28/08/2026 (P_MB9 review Feature 1) — trước đó `ForgotPasswordScreen` gọi thẳng
/// `authRepositoryProvider` từ View, bỏ qua tầng ViewModel (vi phạm layering). Tách ra đây
/// đúng kiến trúc `AuthViewModel`/`ProfileViewModel` đã dùng.
class ForgotPasswordState {
  const ForgotPasswordState({
    this.isSending = false,
    this.errorMessage,
    this.sent = false,
  });

  final bool isSending;
  final String? errorMessage;

  /// AF-01: true dù thông tin nhập đúng hay sai — backend luôn trả cùng một câu, không có
  /// gì để phân biệt ở tầng này (chống dò tài khoản).
  final bool sent;

  ForgotPasswordState copyWith({
    bool? isSending,
    String? errorMessage,
    bool? sent,
    bool clearError = false,
  }) {
    return ForgotPasswordState(
      isSending: isSending ?? this.isSending,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
      sent: sent ?? this.sent,
    );
  }
}

class ForgotPasswordViewModel extends StateNotifier<ForgotPasswordState> {
  ForgotPasswordViewModel(this._ref) : super(const ForgotPasswordState());

  final Ref _ref;

  /// Gọi sau khi View đã tự kiểm định dạng số điện thoại (AF-01: chỉ kiểm HÌNH DẠNG,
  /// không kiểm số/email có tồn tại hay không — việc đó thuộc backend và phải im lặng).
  Future<void> submit({required String phoneNumber, required String email}) async {
    state = state.copyWith(isSending: true, clearError: true);
    try {
      await _ref
          .read(authRepositoryProvider)
          .requestPasswordReset(phoneNumber: phoneNumber, email: email);

      state = state.copyWith(isSending: false, sent: true);
    } on ApiException catch (e) {
      state = state.copyWith(isSending: false, errorMessage: e.message);
    }
  }
}

final forgotPasswordViewModelProvider =
    StateNotifierProvider<ForgotPasswordViewModel, ForgotPasswordState>(
        (ref) => ForgotPasswordViewModel(ref));
