import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
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
  });

  final AuthSession? session;
  final bool isLoading;
  final String? errorMessage;

  bool get isSignedIn => session != null;

  AuthState copyWith({
    AuthSession? session,
    bool? isLoading,
    String? errorMessage,
    bool clearError = false,
    bool clearSession = false,
  }) {
    return AuthState(
      session: clearSession ? null : (session ?? this.session),
      isLoading: isLoading ?? this.isLoading,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
    );
  }
}

class AuthViewModel extends StateNotifier<AuthState> {
  AuthViewModel(this._ref) : super(const AuthState());

  final Ref _ref;

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
      // Hồ sơ khám (Module 04) cũng phải bị vứt — cùng lý do, mở rộng 15/08/2026 từ
      // signOut()/handleSessionExpired() (đã có sẵn từ 14/08/2026) sang đây: nếu không, patient
      // B đăng nhập ngay sau patient A vẫn thấy dữ liệu y tế của A cho tới khi tự pull-to-refresh.
      _ref.invalidate(medicalRecordListViewModelProvider);
      _ref.invalidate(medicalRecordDetailViewModelProvider);

      state = state.copyWith(session: session, isLoading: false);
      return true;
    } on ApiException catch (e) {
      state = state.copyWith(isLoading: false, errorMessage: e.message);
      return false;
    }
  }

  /// Gọi sau khi đổi mật khẩu thành công — thay TOÀN BỘ session bằng bản mới AuthRepository
  /// vừa trả về (kèm access token mới), không chỉ gỡ cờ mustChangePassword trên session cũ.
  /// Token cũ vẫn mang claim MustChangePassword nên phải bị thay hẳn, không thể sửa tại chỗ.
  void applySession(AuthSession session) {
    state = state.copyWith(session: session);
  }

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
  }

  void clearError() => state = state.copyWith(clearError: true);
}

final authViewModelProvider =
    StateNotifierProvider<AuthViewModel, AuthState>((ref) => AuthViewModel(ref));
