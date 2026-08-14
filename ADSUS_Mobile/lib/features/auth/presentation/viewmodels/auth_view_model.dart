import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../data/repositories/biometric_service.dart';
import '../../domain/entities/auth_session.dart';
import '../../../medication_reminder/presentation/viewmodels/intake_view_model.dart';
import '../../../medical_record/presentation/viewmodels/medical_record_detail_viewmodel.dart';
import '../../../medical_record/presentation/viewmodels/medical_record_list_viewmodel.dart';
import '../../../../shared/reminder_preference_store.dart';
import 'profile_view_model.dart';

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
      // Vứt hồ sơ của người đăng nhập trước đi. Không có dòng này thì màn Hồ sơ cá nhân
      // vẫn còn tên, email, ngày sinh của người cũ cho tới khi máy chủ trả về dữ liệu mới.
      _ref.invalidate(profileViewModelProvider);

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

      // Hồ sơ là của người khác trước đó thì phải bỏ đi, không để lẫn.
      _ref.invalidate(profileViewModelProvider);

      state = state.copyWith(
        isLoading: false,
        session: AuthSession(
          accessToken: token,
          fullName: profile.fullName,
          email: profile.email,
          role: profile.role,
          // Phải lấy đúng cờ từ máy chủ, KHÔNG được để cứng false. Admin cấp lại mật khẩu
          // cho tài khoản đã bật sẵn vân tay là cờ này bật lên; để cứng false thì quét vân
          // tay xong vào thẳng ứng dụng, bỏ qua màn ép đổi mật khẩu (UC-25).
          mustChangePassword: profile.mustChangePassword,
        ),
      );
      return true;
    } on ApiException {
      // Token chết (hết hạn, hoặc tài khoản đã bị khoá — AF-02). signOut xoá luôn ghép đôi
      // sinh trắc học, rồi nạp lại trạng thái để nút vân tay biến mất thay vì cứ hiện ra
      // và báo lỗi mỗi lần bấm.
      await repo.signOut();
      state = state.copyWith(
        isLoading: false,
        errorMessage: 'Phiên đăng nhập đã hết hạn. Vui lòng nhập mật khẩu.',
      );
      await _loadBiometricStatus();
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

  /// Máy chủ đã từ chối token đang dùng — hết hạn, hoặc tài khoản vừa bị Admin khoá.
  ///
  /// Khác [signOut] ở chỗ người dùng KHÔNG chủ động bấm gì, nên phải nói rõ lý do, nếu không
  /// họ chỉ thấy ứng dụng tự nhiên nhảy về màn đăng nhập.
  Future<void> handleSessionExpired() async {
    // Đã ở màn đăng nhập rồi thì thôi, tránh xoá đè lên trạng thái đang hiển thị.
    if (!mounted || !state.isSignedIn) return;

    await _ref.read(authRepositoryProvider).signOut();
    _ref.invalidate(profileViewModelProvider);
    _ref.invalidate(intakeLogsProvider);
    _ref.invalidate(reminderPreferenceProvider);
    // Hồ sơ khám (Module 04) cũng phải bị vứt — cùng lý do intakeLogsProvider ở dưới.
    _ref.invalidate(medicalRecordListViewModelProvider);
    _ref.invalidate(medicalRecordDetailViewModelProvider);

    if (!mounted) return;
    state = const AuthState(
      errorMessage: 'Phiên đăng nhập đã kết thúc. Vui lòng đăng nhập lại.',
    );
    await _loadBiometricStatus();
  }

  Future<void> signOut() async {
    await _ref.read(authRepositoryProvider).signOut();

    // Hồ sơ cá nhân phải bị vứt cùng phiên. Nếu không, người đăng nhập kế tiếp trên cùng
    // máy sẽ thấy tên, email và ngày sinh của người trước hiện sẵn trong ô nhập.
    _ref.invalidate(profileViewModelProvider);
    // Lịch thuốc cũng phải bị vứt. MainShell dùng IndexedStack — screen Thuốc không unmount
    // khi đăng xuất, nên autoDispose trên intakeLogsProvider không kích hoạt. Invalidating ở
    // đây đảm bảo user mới luôn nhận đúng dữ liệu riêng, không phải cache user trước.
    _ref.invalidate(intakeLogsProvider);
    _ref.invalidate(reminderPreferenceProvider);
    // Hồ sơ khám (Module 04, SCR-13/SCR-14) — cùng lý do: cả 2 ViewModel đều là
    // NotifierProvider trơn (không .autoDispose), state sống hết vòng đời app. Phát hiện
    // 14/08/2026 qua smoke test thật: đăng nhập tài khoản B rồi vào "Lịch sử khám" vẫn thấy
    // dữ liệu của tài khoản A cho tới khi pull-to-refresh — patient B thoáng thấy dữ liệu y
    // tế của patient A, vi phạm tinh thần GB-05 dù chỉ là dữ liệu của A chứ không phải AI thô.
    _ref.invalidate(medicalRecordListViewModelProvider);
    _ref.invalidate(medicalRecordDetailViewModelProvider);

    state = const AuthState();
    await _loadBiometricStatus();
  }

  void clearError() => state = state.copyWith(clearError: true);
}

final authViewModelProvider =
    StateNotifierProvider<AuthViewModel, AuthState>((ref) => AuthViewModel(ref));
