import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../domain/entities/intake_log.dart';

/// Trạng thái màn nhắc thuốc (SCR-19).
///
/// [errorMessage] + [isSubmittingIds] (Set) cho nút "Đã uống" — set lưu intakeId
/// đang được confirm. Trước đây dùng `bool isSubmitting` global → khi user bấm
/// xác nhận 1 liều thì TẤT CẢ các liều khác cũng vào trạng thái spinner (review
/// 16/08/2026). Set phân biệt được từng intakeId, mỗi card chỉ quay khi chính nó
/// được bấm.
class IntakeListState {
  const IntakeListState({
    this.errorMessage,
    this.isSubmittingIds = const <String>{},
  });

  /// Thông báo lỗi tiếng Việt để hiển thị (vd: đã uống thất bại).
  final String? errorMessage;

  /// Tập intakeId đang gọi confirmIntake. Card nào có id trong set → spinner;
  /// card khác vẫn hiển thị nút "ĐÃ UỐNG" bình thường.
  final Set<String> isSubmittingIds;

  bool isSubmittingFor(String intakeId) => isSubmittingIds.contains(intakeId);

  IntakeListState copyWith({
    String? errorMessage,
    Set<String>? isSubmittingIds,
    bool clearError = false,
  }) =>
      IntakeListState(
        errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
        isSubmittingIds: isSubmittingIds ?? this.isSubmittingIds,
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
    final next = {...state.isSubmittingIds, intakeId};
    state = state.copyWith(isSubmittingIds: next, clearError: true);
    try {
      await _ref.read(medicationIntakeRepositoryProvider).confirmIntake(intakeId);
      // T-6.2: Sau khi confirm thành công → cập nhật widget ngay.
      _ref.read(widgetSyncServiceProvider).triggerSync();
      // Server đã chuyển status → TAKEN (§22.2 fix #7). Refetch lại danh sách để UI
      // đồng bộ. Optimistic update ở tầng use-prescriptions Web làm tương tự.
      _ref.invalidate(intakeLogsProvider);
      // family provider cũng cần invalidate, vì IntakeLog có thể thuộc 1 đơn cụ thể.
      _ref.invalidate(intakeLogsByPrescriptionProvider);
      final after = {...state.isSubmittingIds}..remove(intakeId);
      state = state.copyWith(isSubmittingIds: after);
      return true;
    } on ApiException catch (e) {
      final after = {...state.isSubmittingIds}..remove(intakeId);
      state = state.copyWith(isSubmittingIds: after, errorMessage: e.message);
      return false;
    } catch (e) {
      final after = {...state.isSubmittingIds}..remove(intakeId);
      state = state.copyWith(
        isSubmittingIds: after,
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