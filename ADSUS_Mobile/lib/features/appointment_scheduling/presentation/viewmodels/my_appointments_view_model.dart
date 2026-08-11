import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../domain/entities/appointment.dart';
import '../../domain/services/calendar_sync_service.dart';

/// Trạng thái màn Lịch hẹn của tôi (SCR-22, UC-14).
///
/// Hai luồng chính: xem danh sách (load) và hủy / đổi lịch (cancel). Đổi lịch về bản
/// chất là hủy với lý do cố định "Reschedule" rồi nhảy về màn Đặt lịch — flow đó UI
/// tự xử lý, ViewModel chỉ trả về [cancelledId] để View biết nên điều hướng.
class MyAppointmentsState {
  const MyAppointmentsState({
    this.appointments = const [],
    this.isLoading = false,
    this.isMutating = false,
    this.errorMessage,
    this.cancelledId,
    this.syncedIds = const <String>{},
  });

  final List<Appointment> appointments;
  final bool isLoading;

  /// Đang hủy hoặc đổi lịch — khoá cả 2 nút trên card.
  final bool isMutating;

  final String? errorMessage;

  /// Set khi vừa hủy/đổi thành công, View dùng để navigate hoặc hiển thị snackbar.
  final String? cancelledId;

  /// Tập các appointment đã từng sync vào Calendar trên thiết bị này (UC-16, spec #54).
  /// Bookkeeping local — View dùng để đổi icon từ `event_note` → `event_available`.
  /// Không đảm bảo event còn trong Calendar của user (spec nói rõ one-way, no read-back).
  final Set<String> syncedIds;

  MyAppointmentsState copyWith({
    List<Appointment>? appointments,
    bool? isLoading,
    bool? isMutating,
    String? errorMessage,
    String? cancelledId,
    Set<String>? syncedIds,
    bool clearError = false,
    bool clearCancelledId = false,
  }) {
    return MyAppointmentsState(
      appointments: appointments ?? this.appointments,
      isLoading: isLoading ?? this.isLoading,
      isMutating: isMutating ?? this.isMutating,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
      cancelledId:
          clearCancelledId ? null : (cancelledId ?? this.cancelledId),
      syncedIds: syncedIds ?? this.syncedIds,
    );
  }
}

class MyAppointmentsViewModel extends Notifier<MyAppointmentsState> {
  @override
  MyAppointmentsState build() {
    Future.microtask(load);
    return const MyAppointmentsState(isLoading: true);
  }

  Future<void> load() async {
    state = state.copyWith(isLoading: true, clearError: true);
    try {
      final repo = ref.read(appointmentRepositoryProvider);
      final summaries = await repo.listMyAppointments();

      // Backend đã include slot info trong summary.
      // Convert summaries sang Appointments để tái sử dụng UI component.
      final details = summaries
          .map((s) => Appointment(
                id: s.id,
                slotId: s.slotId,
                patientProfileId: '',
                status: s.status,
                reason: s.reason,
                cancelledReason: s.cancelledReason,
                createdAt: s.createdAt,
                updatedAt: s.createdAt,
                slotDate: s.slotDate,
                startTime: s.startTime,
                endTime: s.endTime,
                doctorName: s.doctorName,
              ))
          .toList();

      // Booked lên trước, Cancelled xuống dưới; trong cùng nhóm sắp theo slotDate ↓.
      details.sort((a, b) {
        if (a.isBooked != b.isBooked) return a.isBooked ? -1 : 1;
        final aDate = a.slotDate ?? a.createdAt;
        final bDate = b.slotDate ?? b.createdAt;
        return bDate.compareTo(aDate);
      });

      // UC-16 — hydrate cờ "đã sync Calendar" cho mỗi appointment Booked. Cancelled
      // thì không cần check vì UI không hiển thị icon sync cho card Cancelled.
      // Nếu service chưa sẵn sàng thì set rỗng — lần mở app sau vẫn load đúng.
      final synced = <String>{};
      try {
        final svc = await ref.read(calendarSyncServiceProvider.future);
        for (final ap in details.where((a) => a.isBooked)) {
          if (await svc.hasSynced(ap.id)) synced.add(ap.id);
        }
      } on Object {
        // Bỏ qua — UI vẫn hoạt động bình thường, chỉ là badge sync sẽ hiển thị sai.
      }

      state = state.copyWith(
        appointments: details,
        isLoading: false,
        syncedIds: synced,
      );
      debugPrint('[DEBUG] MyAppointments: load() completed with ${details.length} appointments');
    } on ApiException catch (e) {
      debugPrint('[DEBUG] MyAppointments: ApiException: ${e.message}');
      state = state.copyWith(isLoading: false, errorMessage: e.message);
    } catch (e, st) {
      debugPrint('[DEBUG] MyAppointments: UNEXPECTED ERROR = $e');
      debugPrint('[DEBUG] MyAppointments: stack = $st');
      state = state.copyWith(isLoading: false, errorMessage: e.toString());
    }
  }

  /// UC-14 bước 4-5 — hủy một cuộc hẹn đang Booked.
  Future<void> cancel({required String id, required String reason}) async {
    state = state.copyWith(isMutating: true, clearError: true);
    try {
      final updated = await ref
          .read(appointmentRepositoryProvider)
          .cancelMyAppointment(id: id, cancellationReason: reason);
      // Patch trực tiếp card trong list — không cần load lại toàn bộ.
      final patched = state.appointments
          .map((a) => a.id == id ? updated : a)
          .toList(growable: false);
      // Best-effort dọn cờ sync — nếu user từng sync appointment này vào Calendar thì
      // event đó giờ đã trở nên vô nghĩa (cuộc hẹn không còn hiệu lực). Nếu service
      // chưa sẵn sàng hoặc throw thì bỏ qua — appointment vẫn cancel bình thường.
      final nextSynced = Set<String>.of(state.syncedIds)..remove(id);
      state = state.copyWith(
        appointments: patched,
        isMutating: false,
        cancelledId: id,
        syncedIds: nextSynced,
      );
      try {
        final svc = await ref.read(calendarSyncServiceProvider.future);
        await svc.clearSyncFlag(id);
      } on Object {
        // Im lặng — UI đã phản ánh cancel rồi.
      }
    } on ApiException catch (e) {
      state = state.copyWith(isMutating: false, errorMessage: e.message);
    }
  }

  /// UC-14 AF-01 — đổi lịch = hủy cũ với lý do "Reschedule" + đặt cái mới (UC-13).
  ///
  /// Trả về true nếu hủy thành công; View sẽ navigate sang BookAppointmentScreen.
  Future<bool> reschedule(Appointment ap) async {
    state = state.copyWith(isMutating: true, clearError: true);
    try {
      await ref.read(appointmentRepositoryProvider).cancelMyAppointment(
            id: ap.id,
            cancellationReason: 'Reschedule',
          );
      final patched = state.appointments
          .map((a) => a.id == ap.id
              ? Appointment(
                  id: a.id,
                  slotId: a.slotId,
                  patientProfileId: a.patientProfileId,
                  status: AppointmentStatus.cancelled,
                  cancelledReason: 'Reschedule',
                  reason: a.reason,
                  createdAt: a.createdAt,
                  updatedAt: DateTime.now(),
                  slotDate: a.slotDate,
                  startTime: a.startTime,
                  endTime: a.endTime,
                  doctorName: a.doctorName,
                )
              : a)
          .toList(growable: false);
      state = state.copyWith(
        appointments: patched,
        isMutating: false,
        cancelledId: ap.id,
      );
      return true;
    } on ApiException catch (e) {
      state = state.copyWith(isMutating: false, errorMessage: e.message);
      return false;
    }
  }

  /// Clear cờ cancelledId sau khi View đã snackbar xong.
  void clearCancelledFlag() {
    if (state.cancelledId != null) {
      state = state.copyWith(clearCancelledId: true);
    }
  }

  void clearError() {
    if (state.errorMessage != null) {
      state = state.copyWith(clearError: true);
    }
  }

  /// UC-16 — đánh dấu appointment đã được sync vào Calendar trên thiết bị này.
  ///
  /// View gọi method này sau khi [CalendarSyncService.addAppointmentToCalendar] trả
  /// về `true`. Cờ lưu trong state để UI đổi icon; persistence thực sự nằm trong
  /// service (SharedPreferences) — service tự lưu khi add thành công, method này chỉ
  /// đồng bộ hoá UI.
  void markSynced(String appointmentId) {
    if (state.syncedIds.contains(appointmentId)) return;
    final next = Set<String>.of(state.syncedIds)..add(appointmentId);
    state = state.copyWith(syncedIds: next);
  }
}

final myAppointmentsViewModelProvider =
    NotifierProvider<MyAppointmentsViewModel, MyAppointmentsState>(
  MyAppointmentsViewModel.new,
);
