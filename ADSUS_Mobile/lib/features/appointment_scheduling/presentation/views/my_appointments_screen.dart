import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_theme.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../domain/entities/appointment.dart';
import '../../domain/services/calendar_sync_service.dart';
import '../viewmodels/book_appointment_view_model.dart';
import '../viewmodels/my_appointments_view_model.dart';
import 'book_appointment_screen.dart';
import 'widgets/appointment_card.dart';
import 'widgets/cancel_reason_sheet.dart';

/// SCR-22 — Màn Lịch hẹn của tôi (UC-14).
///
/// Mỗi thẻ Booked có hai nút:
///   - "Đổi lịch" → hủy bản ghi cũ với lý do "Reschedule", chuyển sang Đặt lịch (UC-13).
///   - "Hủy lịch"  → mở bottom sheet chọn lý do (BR-02 bắt buộc), rồi gọi API hủy.
class MyAppointmentsScreen extends ConsumerStatefulWidget {
  const MyAppointmentsScreen({super.key});

  @override
  ConsumerState<MyAppointmentsScreen> createState() =>
      _MyAppointmentsScreenState();
}

class _MyAppointmentsScreenState
    extends ConsumerState<MyAppointmentsScreen> {
  bool _snackbarShown = false;

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(myAppointmentsViewModelProvider);

    ref.listen<MyAppointmentsState>(myAppointmentsViewModelProvider,
        (prev, next) {
      if (_snackbarShown) return;
      if (next.cancelledId != null) {
        _snackbarShown = true;
        final wasBooked = prev?.appointments
                .firstWhere((a) => a.id == next.cancelledId,
                    orElse: () => _placeholder())
                .isBooked ??
            false;
        WidgetsBinding.instance.addPostFrameCallback((_) async {
          if (!mounted) return;
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
              content: Text(wasBooked
                  ? 'Đã hủy lịch hẹn thành công.'
                  : 'Đã hủy lịch cũ. Vui lòng chọn khung giờ mới.'),
              backgroundColor: AppColors.teal,
            ),
          );
          ref
              .read(myAppointmentsViewModelProvider.notifier)
              .clearCancelledFlag();
          _snackbarShown = false;
        });
      }
      if (next.errorMessage != null) {
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (!mounted) return;
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
              content: Text(next.errorMessage!),
              backgroundColor: AppColors.danger,
            ),
          );
          ref.read(myAppointmentsViewModelProvider.notifier).clearError();
        });
      }
    });

    return Scaffold(
      appBar: AppBar(title: const Text('Lịch hẹn của tôi')),
      body: SafeArea(child: _buildBody(state)),
    );
  }

  Appointment _placeholder() => Appointment(
        id: '',
        slotId: '',
        patientProfileId: '',
        status: AppointmentStatus.cancelled,
        createdAt: DateTime.now(),
        updatedAt: DateTime.now(),
      );

  Widget _buildBody(MyAppointmentsState state) {
    if (state.isLoading && state.appointments.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }

    // Hiện lỗi nếu có
    if (state.errorMessage != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.error_outline, size: 64, color: AppColors.danger),
              const SizedBox(height: 16),
              Text(
                state.errorMessage!,
                textAlign: TextAlign.center,
                style: const TextStyle(fontSize: 15, color: AppColors.danger),
              ),
              const SizedBox(height: 24),
              ElevatedButton.icon(
                onPressed: () => ref.read(myAppointmentsViewModelProvider.notifier).load(),
                icon: const Icon(Icons.refresh),
                label: const Text('TẢI LẠI'),
              ),
            ],
          ),
        ),
      );
    }

    if (state.appointments.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.event_busy, size: 64, color: AppColors.muted),
              const SizedBox(height: 16),
              const Text(
                'Bạn chưa có lịch hẹn nào.',
                style: TextStyle(fontSize: 15, color: AppColors.muted),
              ),
              const SizedBox(height: 24),
              ElevatedButton.icon(
                onPressed: () => _goBook(context),
                icon: const Icon(Icons.add),
                label: const Text('ĐẶT LỊCH NGAY'),
              ),
            ],
          ),
        ),
      );
    }

    debugPrint('[DEBUG] MyAppointmentsScreen: building ListView with ${state.appointments.length} items');
    return Column(
      children: [
        // Header
        Padding(
          padding: const EdgeInsets.all(16),
          child: Text(
            'Bạn có ${state.appointments.length} lịch hẹn',
            style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: AppColors.navy),
          ),
        ),
        // List
        Expanded(
          child: RefreshIndicator(
            onRefresh: () => ref.read(myAppointmentsViewModelProvider.notifier).load(),
            child: ListView.builder(
              padding: const EdgeInsets.fromLTRB(20, 0, 20, 32),
              itemCount: state.appointments.length,
              itemBuilder: (context, i) {
                final ap = state.appointments[i];
                return Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: AppointmentCard(
                    appointment: ap,
                    busy: state.isMutating,
                    onCancel: () => _onCancel(context, ap),
                    onReschedule: () => _onReschedule(context, ap),
                    onSyncCalendar: () => _onSyncCalendar(context, ap),
                    syncedToCalendar: state.syncedIds.contains(ap.id),
                  ),
                );
              },
            ),
          ),
        ),
      ],
    );
  }

  Future<void> _onCancel(BuildContext context, Appointment ap) async {
    final reason = await showCancelReasonSheet(context);
    if (reason == null || reason.isEmpty) return;
    if (!mounted) return;
    await ref
        .read(myAppointmentsViewModelProvider.notifier)
        .cancel(id: ap.id, reason: reason);
  }

  Future<void> _onReschedule(BuildContext context, Appointment ap) async {
    // Hiện dialog xác nhận trước khi đặt lịch mới
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Xác nhận đặt lịch mới'),
        content: const Text(
          'Bạn có muốn đặt lịch mới không?\n'
          'Lịch hẹn hiện tại sẽ bị hủy.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Hủy bỏ'),
          ),
          ElevatedButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Xác nhận đặt lịch'),
          ),
        ],
      ),
    );

    if (confirmed != true || !mounted) return;

    // Tiến hành hủy lịch cũ và mở màn đặt lịch
    final ok = await ref
        .read(myAppointmentsViewModelProvider.notifier)
        .reschedule(ap);
    if (!ok || !mounted) return;

    _goBook(context);
  }

  /// UC-16 — bấm "Thêm vào lịch" trên một appointment Booked.
  ///
  /// Flow: gọi [CalendarSyncService.addAppointmentToCalendar] (mở native Calendar
  /// dialog), nếu thành công thì đánh dấu state.syncedIds để UI đổi icon ngay. Nếu
  /// thiếu dữ liệu slotDate/startTime/endTime (rất hiếm — summary list đã gọi
  /// getMyAppointment fill đủ), báo snackbar yêu cầu mở chi tiết trước.
  Future<void> _onSyncCalendar(BuildContext context, Appointment ap) async {
    CalendarSyncService service;
    try {
      service =
          await ref.read(calendarSyncServiceProvider.future);
    } on Object catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Không khởi tạo được dịch vụ lịch: $e'),
          backgroundColor: AppColors.danger,
        ),
      );
      return;
    }

    try {
      final ok = await service.addAppointmentToCalendar(ap);
      if (!mounted) return;
      if (ok) {
        ref
            .read(myAppointmentsViewModelProvider.notifier)
            .markSynced(ap.id);
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Đã mở ứng dụng Lịch — vui lòng xác nhận thêm.'),
            backgroundColor: AppColors.teal,
          ),
        );
      } else {
        // User huỷ hoặc OS không có Calendar app. Không báo lỗi — chỉ im lặng hoặc
        // snackbar nhẹ để user biết.
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Chưa thêm — bạn có thể thử lại bất cứ lúc nào.'),
          ),
        );
      }
    } on CalendarSyncException catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(e.message),
          backgroundColor: AppColors.danger,
        ),
      );
    }
  }

  void _goBook(BuildContext context) {
    // Reset TẤT CẢ state trước khi navigate để không bị snackbar hiện lại
    final notifier = ref.read(bookAppointmentViewModelProvider.notifier);
    notifier.resetForNewBooking();
    notifier.clearBookingSuccess();
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => const BookAppointmentScreen(),
      ),
    );
  }
}
