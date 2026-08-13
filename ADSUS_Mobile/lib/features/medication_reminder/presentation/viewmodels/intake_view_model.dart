import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../domain/entities/intake_log.dart';

/// Trạng thái màn nhắc thuốc (SCR-19).
///
/// Chỉ cần 1 trường [errorMessage] + [isSubmitting] cho nút "Đã uống" — danh sách
/// intake log lấy qua FutureProvider bên dưới, không lưu ở đây.
class IntakeListState {
  const IntakeListState({
    this.errorMessage,
    this.isSubmitting = false,
  });

  /// Thông báo lỗi tiếng Việt để hiển thị (vd: đã uống thất bại).
  final String? errorMessage;

  /// Đang gọi confirmIntake — disable nút "Đã uống" cho log đó.
  final bool isSubmitting;

  IntakeListState copyWith({
    String? errorMessage,
    bool? isSubmitting,
    bool clearError = false,
  }) =>
      IntakeListState(
        errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
        isSubmitting: isSubmitting ?? this.isSubmitting,
      );
}

/// Cho widget SCR-19 lấy danh sách intake log.
///
/// `autoDispose` để provider bị dispose khi không còn widget listening (logout / navigate
/// away), đảm bảo user mới login luôn trigger refetch thay vì dùng cache user cũ.
/// Xác nhận uống thành công → `invalidate` gọi refetch bình thường.
final intakeLogsProvider = FutureProvider.autoDispose<List<IntakeLog>>((ref) async {
  return ref.watch(medicationIntakeRepositoryProvider).getMyIntakeLogs();
});

/// Lịch uống của 1 đơn cụ thể — dùng cho màn chi tiết đơn (SCR-19 lọc theo đơn).
final intakeLogsByPrescriptionProvider =
    FutureProvider.family<List<IntakeLog>, String>((ref, prescriptionId) async {
  return ref
      .watch(medicationIntakeRepositoryProvider)
      .getIntakeLogsByPrescription(prescriptionId);
});

/// ViewModel xử lý confirmIntake + invalidate các query liên quan.
class IntakeListViewModel extends StateNotifier<IntakeListState> {
  IntakeListViewModel(this._ref) : super(const IntakeListState());

  final Ref _ref;

  Future<bool> confirmIntake(String intakeId) async {
    state = state.copyWith(isSubmitting: true, clearError: true);
    try {
      await _ref.read(medicationIntakeRepositoryProvider).confirmIntake(intakeId);
      // Server đã chuyển status → TAKEN (§22.2 fix #7). Refetch lại danh sách để UI
      // đồng bộ. Optimistic update ở tầng use-prescriptions Web làm tương tự.
      _ref.invalidate(intakeLogsProvider);
      // family provider cũng cần invalidate, vì IntakeLog có thể thuộc 1 đơn cụ thể.
      _ref.invalidate(intakeLogsByPrescriptionProvider);
      state = state.copyWith(isSubmitting: false);
      return true;
    } on ApiException catch (e) {
      state = state.copyWith(isSubmitting: false, errorMessage: e.message);
      return false;
    } catch (e) {
      state = state.copyWith(
        isSubmitting: false,
        errorMessage: 'Không ghi nhận được việc uống thuốc.',
      );
      return false;
    }
  }

  void clearError() => state = state.copyWith(clearError: true);
}

final intakeListViewModelProvider =
    StateNotifierProvider<IntakeListViewModel, IntakeListState>((ref) {
  return IntakeListViewModel(ref);
});