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
import 'widgets/edit_clinical_info_sheet.dart';

/// SCR-22 — Màn Lịch khám của tôi (UC-14).
///
/// Phân tách 2 tab: "Lịch của tôi" và "Lịch người thân" kèm số lượng.
/// Cảnh báo trước khi thực hiện lần hủy thứ 3 trong ngày.
class MyAppointmentsScreen extends ConsumerStatefulWidget {
  const MyAppointmentsScreen({
    super.key,
    this.highlightAppointmentId, // Dùng để scroll đến appointment cụ thể từ notification
  });

  /// Appointment ID cần highlight/scroll đến (từ notification tap)
  final String? highlightAppointmentId;

  @override
  ConsumerState<MyAppointmentsScreen> createState() =>
      _MyAppointmentsScreenState();
}

class _MyAppointmentsScreenState
    extends ConsumerState<MyAppointmentsScreen> {
  bool _snackbarShown = false;
  final ScrollController _scrollController = ScrollController();

  @override
  void initState() {
    super.initState();
    // Sau khi data load xong, scroll đến appointment được highlight
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _scrollToHighlightedAppointment();
    });
  }

  @override
  void dispose() {
    _scrollController.dispose();
    super.dispose();
  }

  void _scrollToHighlightedAppointment() {
    if (widget.highlightAppointmentId == null) return;

    final state = ref.read(myAppointmentsViewModelProvider);
    final index = state.filteredAppointments.indexWhere(
      (a) => a.id == widget.highlightAppointmentId,
    );

    if (index != -1 && _scrollController.hasClients) {
      // Tính offset của item (approximate)
      const itemHeight = 120.0; // Chiều cao approximated của mỗi card
      final offset = index * itemHeight;
      _scrollController.animateTo(
        offset,
        duration: const Duration(milliseconds: 300),
        curve: Curves.easeInOut,
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(myAppointmentsViewModelProvider);

    // Listen để scroll khi data đã load xong
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
                  ? 'Đã hủy lịch khám thành công.'
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
      appBar: AppBar(title: const Text('Lịch khám của tôi')),
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
    if (state.errorMessage != null && state.appointments.isEmpty) {
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

    final filteredList = state.filteredAppointments;

    return Column(
      children: [
        // Tab SegmentedButton: [Lịch của tôi (X)] [Lịch người thân (Y)]
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
          child: SizedBox(
            width: double.infinity,
            child: SegmentedButton<String>(
              segments: [
                ButtonSegment<String>(
                  value: 'SELF',
                  label: Text('Lịch của tôi (${state.selfCount})'),
                  icon: const Icon(Icons.person, size: 16),
                ),
                ButtonSegment<String>(
                  value: 'RELATIVE',
                  label: Text('Lịch người thân (${state.relativeCount})'),
                  icon: const Icon(Icons.people, size: 16),
                ),
              ],
              selected: {state.filterScope},
              onSelectionChanged: (newSelection) {
                ref
                    .read(myAppointmentsViewModelProvider.notifier)
                    .setFilterScope(newSelection.first);
              },
            ),
          ),
        ),

        // List or Empty state
        Expanded(
          child: filteredList.isEmpty
              ? _buildEmptyState(state.filterScope)
              : RefreshIndicator(
                  onRefresh: () =>
                      ref.read(myAppointmentsViewModelProvider.notifier).load(),
                  child: ListView.builder(
                    controller: _scrollController,
                    padding: const EdgeInsets.fromLTRB(20, 8, 20, 32),
                    itemCount: filteredList.length,
                    itemBuilder: (context, i) {
                      final ap = filteredList[i];
                      // Highlight nếu đây là appointment được tap từ notification
                      final isHighlighted = ap.id == widget.highlightAppointmentId;
                      return AnimatedContainer(
                        duration: const Duration(milliseconds: 500),
                        margin: const EdgeInsets.only(bottom: 12),
                        decoration: isHighlighted
                            ? BoxDecoration(
                                border: Border.all(color: AppColors.teal, width: 2),
                                borderRadius: BorderRadius.circular(12),
                              )
                            : null,
                        child: AppointmentCard(
                          appointment: ap,
                          busy: state.isMutating,
                          onCancel: () => _onCancel(ap),
                          onReschedule: () => _onReschedule(ap),
                          onEditClinicalInfo: () => _onEditClinicalInfo(ap),
                          onSyncCalendar: () => _onSyncCalendar(ap),
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

  Widget _buildEmptyState(String filterScope) {
    final isSelf = filterScope == 'SELF';
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              isSelf ? Icons.event_busy : Icons.people_outline,
              size: 64,
              color: AppColors.muted,
            ),
            const SizedBox(height: 16),
            Text(
              isSelf
                  ? 'Bạn chưa có lịch khám nào cho bản thân.'
                  : 'Bạn chưa có lịch khám nào đặt cho người thân.',
              textAlign: TextAlign.center,
              style: const TextStyle(fontSize: 15, color: AppColors.muted),
            ),
            const SizedBox(height: 24),
            ElevatedButton.icon(
              onPressed: () => _goBook(context),
              icon: const Icon(Icons.add),
              label: Text(isSelf ? 'ĐẶT LỊCH NGAY' : 'ĐẶT LỊCH CHO NGƯỜI THÂN'),
              style: ElevatedButton.styleFrom(
                backgroundColor: AppColors.teal,
                foregroundColor: Colors.white,
                padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
              ),
            ),
          ],
        ),
      ),
    );
  }

  void _onEditClinicalInfo(Appointment ap) {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (context) => EditClinicalInfoSheet(
        appointment: ap,
        onUpdated: (updated) {
          ref.read(myAppointmentsViewModelProvider.notifier).load();
        },
      ),
    );
  }

  Future<void> _onCancel(Appointment ap) async {
    // Kiểm tra số lần hủy hôm nay
    final status = await ref
        .read(myAppointmentsViewModelProvider.notifier)
        .checkCancellationStatus();

    if (!mounted) return;

    if (status != null && status.isNextCancellationFinal) {
      final proceed = await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          icon: const Icon(Icons.warning_amber_rounded, color: Colors.orange, size: 48),
          title: const Text(
            'Cảnh báo lượt hủy cuối',
            style: TextStyle(fontWeight: FontWeight.bold, color: Colors.orange),
          ),
          content: const Text(
            'Bạn đã hủy 2 lần trong ngày hôm nay.\n\n'
            'Nếu bạn hủy lần này (lần thứ 3), quyền tự đặt lịch trực tuyến của bạn sẽ bị tạm khóa đến hết ngày hôm nay. Để đặt lịch sau đó, bạn sẽ phải liên hệ hotline của phòng khám.\n\n'
            'Bạn có chắc chắn muốn tiếp tục hủy không?',
            style: TextStyle(fontSize: 14),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Quay lại'),
            ),
            ElevatedButton(
              onPressed: () => Navigator.pop(context, true),
              style: ElevatedButton.styleFrom(
                backgroundColor: Colors.orange.shade700,
                foregroundColor: Colors.white,
              ),
              child: const Text('Tiếp tục hủy'),
            ),
          ],
        ),
      );

      if (proceed != true || !mounted) return;
    }

    final reason = await showCancelReasonSheet(context);
    if (reason == null || reason.isEmpty) return;
    if (!mounted) return;
    await ref
        .read(myAppointmentsViewModelProvider.notifier)
        .cancel(id: ap.id, reason: reason);
  }

  Future<void> _onReschedule(Appointment ap) async {
    await _showRescheduleDialog(ap);
  }

  Future<void> _showRescheduleDialog(Appointment ap) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Center(
          child: Text('Đặt lịch mới'),
        ),
        content: const Text(
          'Bạn có muốn đặt lịch mới không?\n'
          'Lịch khám hiện tại sẽ bị hủy.',
        ),
        actions: [
          SizedBox(
            width: 120,
            child: OutlinedButton(
              onPressed: () => Navigator.pop(context, false),
              style: OutlinedButton.styleFrom(
                foregroundColor: AppColors.navy,
                padding: const EdgeInsets.symmetric(vertical: 12),
              ),
              child: const Text('Hủy bỏ'),
            ),
          ),
          const SizedBox(width: 8),
          SizedBox(
            width: 120,
            child: ElevatedButton(
              onPressed: () => Navigator.pop(context, true),
              style: ElevatedButton.styleFrom(
                backgroundColor: AppColors.teal,
                foregroundColor: Colors.white,
                padding: const EdgeInsets.symmetric(vertical: 12),
              ),
              child: const Text('Xác nhận'),
            ),
          ),
        ],
        actionsAlignment: MainAxisAlignment.center,
        actionsPadding: const EdgeInsets.only(bottom: 16, left: 16, right: 16, top: 8),
      ),
    );

    if (confirmed != true || !mounted) return;

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
  Future<void> _onSyncCalendar(Appointment ap) async {
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
            content: Text('Đã mở ứng dụng Lịch — xác nhận để thêm lịch khám và 2 lời nhắc (24h, 1h).'),
            backgroundColor: AppColors.teal,
            duration: Duration(seconds: 4),
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
