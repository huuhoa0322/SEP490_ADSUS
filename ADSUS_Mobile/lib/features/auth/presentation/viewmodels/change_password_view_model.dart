import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
import 'auth_view_model.dart';

/// UC-25 — đổi mật khẩu của chính mình.
///
/// Thêm 28/08/2026 (P_MB9 review Feature 1) — trước đó `ChangePasswordScreen` gọi thẳng
/// `authRepositoryProvider` từ View, bỏ qua tầng ViewModel (vi phạm layering, và không có
/// gì để unit-test tách biệt UI cho luồng này). Tách ra đây đúng kiến trúc `AuthViewModel`/
/// `ProfileViewModel` đã dùng.
class ChangePasswordState {
  const ChangePasswordState({
    this.isSaving = false,
    this.errorMessage,
    this.succeeded = false,
  });

  final bool isSaving;
  final String? errorMessage;
  final bool succeeded;

  ChangePasswordState copyWith({
    bool? isSaving,
    String? errorMessage,
    bool? succeeded,
    bool clearError = false,
  }) {
    return ChangePasswordState(
      isSaving: isSaving ?? this.isSaving,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
      succeeded: succeeded ?? this.succeeded,
    );
  }
}

class ChangePasswordViewModel extends StateNotifier<ChangePasswordState> {
  ChangePasswordViewModel(this._ref) : super(const ChangePasswordState());

  final Ref _ref;

  /// Gọi sau khi View đã tự kiểm định dạng (độ dài, chữ hoa, chữ số, xác nhận khớp) —
  /// ViewModel này chỉ lo phần gọi mạng và state kết quả, không lặp lại validate hình dạng.
  ///
  /// currentPassword null khi tài khoản đang bị ép đổi mật khẩu tạm (sửa 06/08/2026) —
  /// xem AuthRepository.changePassword.
  Future<bool> submit({
    required String? currentPassword,
    required String newPassword,
    required String confirmNewPassword,
  }) async {
    state = state.copyWith(isSaving: true, clearError: true, succeeded: false);
    try {
      await _ref.read(authRepositoryProvider).changePassword(
            currentPassword: currentPassword,
            newPassword: newPassword,
            confirmNewPassword: confirmNewPassword,
          );

      // Backend đã gỡ cờ trong DB, gỡ luôn ở client để AuthGuard/router thôi chặn màn khác.
      _ref.read(authViewModelProvider.notifier).clearMustChangePassword();

      state = state.copyWith(isSaving: false, succeeded: true);
      return true;
    } on ApiException catch (e) {
      state = state.copyWith(isSaving: false, errorMessage: e.message);
      return false;
    }
  }

  void clearError() => state = state.copyWith(clearError: true);
}

final changePasswordViewModelProvider =
    StateNotifierProvider<ChangePasswordViewModel, ChangePasswordState>(
        (ref) => ChangePasswordViewModel(ref));
