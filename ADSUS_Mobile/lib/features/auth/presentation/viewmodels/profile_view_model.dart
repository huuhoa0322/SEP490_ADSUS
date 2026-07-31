import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../domain/entities/user_profile.dart';

class ProfileState {
  const ProfileState({
    this.profile,
    this.isLoading = false,
    this.isSaving = false,
    this.errorMessage,
    this.successMessage,
  });

  final UserProfile? profile;
  final bool isLoading;
  final bool isSaving;
  final String? errorMessage;
  final String? successMessage;

  ProfileState copyWith({
    UserProfile? profile,
    bool? isLoading,
    bool? isSaving,
    String? errorMessage,
    String? successMessage,
    bool clearMessages = false,
  }) {
    return ProfileState(
      profile: profile ?? this.profile,
      isLoading: isLoading ?? this.isLoading,
      isSaving: isSaving ?? this.isSaving,
      errorMessage: clearMessages ? null : (errorMessage ?? this.errorMessage),
      successMessage: clearMessages ? null : (successMessage ?? this.successMessage),
    );
  }
}

/// UC-10 — hồ sơ cá nhân, và UC-02 bật/tắt sinh trắc học.
class ProfileViewModel extends StateNotifier<ProfileState> {
  ProfileViewModel(this._ref) : super(const ProfileState());

  final Ref _ref;

  Future<void> load() async {
    state = state.copyWith(isLoading: true, clearMessages: true);
    try {
      final profile = await _ref.read(authRepositoryProvider).getMyProfile();
      state = state.copyWith(profile: profile, isLoading: false);
    } on ApiException catch (e) {
      state = state.copyWith(isLoading: false, errorMessage: e.message);
    }
  }

  /// Chỉ gửi ba trường được phép sửa. Số điện thoại không có ở đây (BR-02).
  Future<bool> save({
    required String fullName,
    String? email,
    String? dateOfBirth,
  }) async {
    state = state.copyWith(isSaving: true, clearMessages: true);
    try {
      final repo = _ref.read(authRepositoryProvider);
      await repo.updateMyProfile(
        fullName: fullName.trim(),
        email: (email == null || email.trim().isEmpty) ? null : email.trim(),
        dateOfBirth: (dateOfBirth == null || dateOfBirth.isEmpty) ? null : dateOfBirth,
      );

      // Đọc lại từ máy chủ thay vì tự sửa state — để màn hình luôn hiển thị đúng thứ
      // đã thực sự lưu, không phải thứ người dùng vừa gõ.
      final fresh = await repo.getMyProfile();
      state = state.copyWith(
        profile: fresh,
        isSaving: false,
        successMessage: 'Đã lưu thay đổi.',
      );
      return true;
    } on ApiException catch (e) {
      state = state.copyWith(isSaving: false, errorMessage: e.message);
      return false;
    }
  }

  /// UC-02 — bật/tắt sinh trắc học.
  Future<bool> setBiometric(bool enabled) async {
    state = state.copyWith(isSaving: true, clearMessages: true);
    try {
      await _ref.read(authRepositoryProvider).setBiometricEnabled(enabled);
      final current = state.profile;
      state = state.copyWith(
        isSaving: false,
        profile: current?.copyWith(biometricEnabled: enabled),
        successMessage: enabled
            ? 'Đã bật đăng nhập bằng vân tay.'
            : 'Đã tắt đăng nhập bằng vân tay.',
      );
      return true;
    } on ApiException catch (e) {
      state = state.copyWith(isSaving: false, errorMessage: e.message);
      return false;
    }
  }

  void clearMessages() => state = state.copyWith(clearMessages: true);
}

final profileViewModelProvider =
    StateNotifierProvider<ProfileViewModel, ProfileState>(
        (ref) => ProfileViewModel(ref));
