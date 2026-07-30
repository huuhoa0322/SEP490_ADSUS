import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../data/repositories/biometric_service.dart';
import '../../domain/entities/auth_session.dart';

/// Trạng thái phiên đăng nhập của toàn ứng dụng.
class AuthState {
  const AuthState({
    this.session,
    this.isLoading = false,
    this.errorMessage,
    this.biometricAvailable = false,
    this.biometricPaired = false,
  });

  final AuthSession? session;
  final bool isLoading;
  final String? errorMessage;

  /// Máy có cảm biến và đã đăng ký mẫu vân tay/khuôn mặt.
  final bool biometricAvailable;

  /// Đã đăng nhập bằng mật khẩu trên máy này và người dùng đã bật tính năng (UC-02 BR-01).
  final bool biometricPaired;

  bool get isSignedIn => session != null;

  /// Chỉ hiện nút đăng nhập vân tay khi thoả cả hai điều kiện.
  bool get canUseBiometric => biometricAvailable && biometricPaired;

  AuthState copyWith({
    AuthSession? session,
    bool? isLoading,
    String? errorMessage,
    bool? biometricAvailable,
    bool? biometricPaired,
    bool clearError = false,
    bool clearSession = false,
  }) {
    return AuthState(
      session: clearSession ? null : (session ?? this.session),
      isLoading: isLoading ?? this.isLoading,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
      biometricAvailable: biometricAvailable ?? this.biometricAvailable,
      biometricPaired: biometricPaired ?? this.biometricPaired,
    );
  }
}

class AuthViewModel extends StateNotifier<AuthState> {
  AuthViewModel(this._ref) : super(const AuthState()) {
    _loadBiometricStatus();
  }

  final Ref _ref;

  Future<void> _loadBiometricStatus() async {
    final available = await _ref.read(biometricServiceProvider).isAvailable();
    final paired = await _ref.read(authRepositoryProvider).isBiometricPaired();
    if (mounted) {
      state = state.copyWith(biometricAvailable: available, biometricPaired: paired);
    }
  }

  /// UC-01 — đăng nhập bằng số điện thoại và mật khẩu.
  Future<bool> signIn(String phoneNumber, String password) async {
    state = state.copyWith(isLoading: true, clearError: true);
    try {
      final session = await _ref.read(authRepositoryProvider).signIn(
            phoneNumber: phoneNumber.trim(),
            password: password,
          );
      state = state.copyWith(session: session, isLoading: false);
      // Đăng nhập bằng mật khẩu xong thì máy này đã được ghép đôi (UC-02 BR-01).
      await _loadBiometricStatus();
      return true;
    } on ApiException catch (e) {
      state = state.copyWith(isLoading: false, errorMessage: e.message);
      return false;
    }
  }

  /// UC-02 — đăng nhập bằng vân tay/khuôn mặt.
  ///
  /// Không gọi API đăng nhập lại: token cũ vẫn nằm trong secure storage, sinh trắc học chỉ
  /// đóng vai trò mở khoá token đó tại máy. Vì vậy vẫn phải gọi getMyProfile để chắc chắn
  /// token còn hiệu lực và tài khoản chưa bị Admin khoá (AF-02).
  Future<bool> signInWithBiometric() async {
    state = state.copyWith(isLoading: true, clearError: true);

    final outcome = await _ref.read(biometricServiceProvider).authenticate();

    if (outcome != BiometricOutcome.success) {
      state = state.copyWith(
        isLoading: false,
        // GB-06: không nói rõ vì sao quét thất bại.
        errorMessage: outcome == BiometricOutcome.unavailable
            ? 'Không dùng được đăng nhập sinh trắc học trên máy này. Vui lòng nhập mật khẩu.'
            : 'Xác thực không thành công. Thử lại hoặc nhập mật khẩu.',
      );
      return false;
    }

    final repo = _ref.read(authRepositoryProvider);
    final token = await repo.readStoredToken();
    if (token == null || token.isEmpty) {
      state = state.copyWith(
        isLoading: false,
        errorMessage: 'Phiên đăng nhập đã hết hạn. Vui lòng nhập mật khẩu.',
      );
      return false;
    }

    try {
      // AF-02: token còn hạn nhưng tài khoản có thể đã bị khoá — backend sẽ từ chối.
      final profile = await repo.getMyProfile();
      state = state.copyWith(
        isLoading: false,
        session: AuthSession(
          accessToken: token,
          fullName: profile.fullName,
          email: profile.email,
          role: profile.role,
          mustChangePassword: false,
        ),
      );
      return true;
    } on ApiException {
      await repo.signOut();
      state = state.copyWith(
        isLoading: false,
        errorMessage: 'Phiên đăng nhập đã hết hạn. Vui lòng nhập mật khẩu.',
      );
      return false;
    }
  }

  /// Gọi sau khi đổi mật khẩu thành công — backend đã gỡ cờ trong DB, client gỡ theo.
  void clearMustChangePassword() {
    final current = state.session;
    if (current == null) return;
    state = state.copyWith(
      session: AuthSession(
        accessToken: current.accessToken,
        fullName: current.fullName,
        email: current.email,
        role: current.role,
        mustChangePassword: false,
      ),
    );
  }

  Future<void> refreshBiometricStatus() => _loadBiometricStatus();

  Future<void> signOut() async {
    await _ref.read(authRepositoryProvider).signOut();
    state = const AuthState();
    await _loadBiometricStatus();
  }

  void clearError() => state = state.copyWith(clearError: true);
}

final authViewModelProvider =
    StateNotifierProvider<AuthViewModel, AuthState>((ref) => AuthViewModel(ref));
