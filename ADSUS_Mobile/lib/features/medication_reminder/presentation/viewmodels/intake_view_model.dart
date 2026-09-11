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

/// Notifier quản lý danh sách intake log.
///
/// Đổi từ `FutureProvider` sang `AsyncNotifierProvider` (review 26/09/2026) để
/// cho phép patch 1 item vào state sau khi `confirmIntake` thành công — UI
/// chuyển card sang "Đã xác nhận" NGAY LẬP TỨC thay vì phải chờ refetch cả list.
class IntakeLogsNotifier extends AutoDisposeAsyncNotifier<List<IntakeLog>> {
  @override
  Future<List<IntakeLog>> build() async {
    return ref.watch(medicationIntakeRepositoryProvider).getMyIntakeLogs();
  }

  /// Patch trạng thái 1 intake log sau khi confirm thành công.
  ///
  /// Gán `confirmedAtUtc` = now và `status` = TAKEN ngay trong memory state —
  /// không cần chờ `invalidate` refetch cả list từ server.
  Future<void> patchConfirmed(String intakeId) async {
    final current = state.valueOrNull;
    if (current == null) return; // Dang loading, invalid, hoac khong co data.

    final patched = current.map((log) {
      if (log.intakeId == intakeId) {
        return log.copyWith(
          confirmedAtUtc: DateTime.now().toUtc(),
          status: IntakeStatus.taken,
        );
      }
      return log;
    }).toList();

    state = AsyncData(patched);
  }
}

/// Provider danh sách intake log.
///
/// `autoDispose` để provider bị dispose khi không còn widget listening (logout /
/// navigate away), đảm bảo user mới login luôn trigger refetch thay vì dùng
/// cache user cũ.
///
/// Sau khi `confirmIntake` thành công → `patchConfirmed` được gọi ngay để UI
/// re-render TỨC THÌ với card "Đã xác nhận". `invalidate` vẫn chạy nền để
/// sync dữ liệu thật từ server (single source of truth).
final intakeLogsProvider =
    AsyncNotifierProvider.autoDispose<IntakeLogsNotifier, List<IntakeLog>>(
        IntakeLogsNotifier.new);

/// Lịch uống của 1 đơn cụ thể — dùng cho màn chi tiết đơn (SCR-19 lọc theo đơn).
final intakeLogsByPrescriptionProvider =
    FutureProvider.family<List<IntakeLog>, String>((ref, prescriptionId) async {
  return ref
      .watch(medicationIntakeRepositoryProvider)
      .getIntakeLogsByPrescription(prescriptionId);
});

/// ViewModel xử lý confirmIntake.
///
/// Sau khi `POST /confirm` thành công:
/// 1. Gọi `patchConfirmed` ngay → UI re-render tức thì (optimistic patch).
/// 2. `widgetSyncService.triggerSync()` → cập nhật Android widget.
/// 3. `invalidate` chạy nền → refetch từ server (single source of truth).
class IntakeListViewModel extends StateNotifier<IntakeListState> {
  IntakeListViewModel(this._ref) : super(const IntakeListState());

  final Ref _ref;

  Future<bool> confirmIntake(String intakeId) async {
    // Client-side guard: MISSED doses cannot be confirmed (GB-01 terminal state).
    // Backend also enforces this, but guard here avoids unnecessary API call.
    final logs = _ref.read(intakeLogsProvider).valueOrNull ?? [];
    final log = logs.where((l) => l.intakeId == intakeId).firstOrNull;
    if (log != null && log.status == IntakeStatus.missed) return false;

    final next = {...state.isSubmittingIds, intakeId};
    state = state.copyWith(isSubmittingIds: next, clearError: true);
    try {
      await _ref.read(medicationIntakeRepositoryProvider).confirmIntake(intakeId);

      // Bước 1: patch UI ngay — user thấy "Đã xác nhận" tức thì.
      _ref.read(intakeLogsProvider.notifier).patchConfirmed(intakeId);

      // Bước 2: cập nhật Android widget.
      _ref.read(widgetSyncServiceProvider).triggerSync();

      // Bước 3: invalidate chạy nền — server là single source of truth.
      // Sau invalidate, state có thể tạm sang AsyncLoading rồi resolve lại.
      _ref.invalidate(intakeLogsProvider);
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
