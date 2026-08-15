import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../data/dtos/health_log_request.dart';
import '../../domain/entities/health_log.dart';

/// Trạng thái màn hình hiển thị nhật ký sức khỏe (Module 9 - FT-35).
class HealthLogState {
  const HealthLogState({
    this.errorMessage,
    this.isSubmitting = false,
  });

  /// Thông báo lỗi tiếng Việt để hiển thị (vd: tạo thất bại).
  final String? errorMessage;

  /// Đang gọi createLog — disable nút gửi.
  final bool isSubmitting;

  HealthLogState copyWith({
    String? errorMessage,
    bool? isSubmitting,
    bool clearError = false,
  }) =>
      HealthLogState(
        errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
        isSubmitting: isSubmitting ?? this.isSubmitting,
      );
}

/// Ngày được chọn để lọc nhật ký — mặc định là hôm nay.
final selectedDateProvider = StateProvider<DateTime>((ref) => DateTime.now());

/// Lấy danh sách nhật ký sức khỏe theo ngày đã chọn.
///
/// `autoDispose` để provider bị dispose khi không còn widget listening (logout / navigate
/// away), đảm bảo user mới login luôn trigger refetch thay vì dùng cache user cũ.
final healthLogsProvider = FutureProvider.autoDispose<List<HealthLog>>((ref) async {
  final selectedDate = ref.watch(selectedDateProvider);
  return ref.watch(healthLogRepositoryProvider).getLogs(date: selectedDate);
});

/// ViewModel xử lý tạo mới nhật ký sức khỏe.
class HealthLogViewModel extends StateNotifier<HealthLogState> {
  HealthLogViewModel(this._ref) : super(const HealthLogState());

  final Ref _ref;

  /// Tạo mới 1 ghi chép sức khỏe.
  ///
  /// Nếu thành công, invalidates [healthLogsProvider] để UI đồng bộ.
  /// Trả về `true` nếu tạo thành công, `false` nếu có lỗi.
  Future<bool> createLog(HealthLogType type, String content, DateTime date) async {
    state = state.copyWith(isSubmitting: true, clearError: true);

    try {
      final request = HealthLogRequest(type: type, content: content, logDate: date);
      debugPrint('[HealthLog] Creating log with type: ${type.value}, content: $content');
      await _ref.read(healthLogRepositoryProvider).createLog(request);
      debugPrint('[HealthLog] Log created successfully');

      // Tạo thành công — refetch danh sách để đồng bộ UI.
      _ref.invalidate(healthLogsProvider);

      state = state.copyWith(isSubmitting: false);
      return true;
    } on ApiException catch (e) {
      debugPrint('[HealthLog] ApiException: ${e.message}');
      state = state.copyWith(isSubmitting: false, errorMessage: e.message);
      return false;
    } catch (e, stack) {
      debugPrint('[HealthLog] Unexpected error: $e\n$stack');
      state = state.copyWith(
        isSubmitting: false,
        errorMessage: 'Không lưu được nhật ký sức khỏe.',
      );
      return false;
    }
  }

  /// Xóa thông báo lỗi hiện tại.
  void clearError() => state = state.copyWith(clearError: true);
}

final healthLogViewModelProvider =
    StateNotifierProvider<HealthLogViewModel, HealthLogState>((ref) {
  return HealthLogViewModel(ref);
});
