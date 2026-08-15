import 'package:dropdown_search/dropdown_search.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_theme.dart';
import '../viewmodels/book_appointment_view_model.dart';
import 'widgets/slot_pill.dart';

/// SCR-21 — Màn đặt lịch khám (UC-13).
///
/// Luồng:
///   1. Tự nạp danh sách OPEN slot khi mở.
///   2. Bệnh nhân chọn bác sĩ (tuỳ chọn) → chọn ngày (bắt buộc) → chọn 1 slot.
///   3. Nhập lý do (tuỳ chọn) → bấm "Xác nhận đặt lịch".
///   4. Khi thành công: SnackBar + tự pop về Home.
class BookAppointmentScreen extends ConsumerStatefulWidget {
  const BookAppointmentScreen({super.key});

  @override
  ConsumerState<BookAppointmentScreen> createState() =>
      _BookAppointmentScreenState();
}

class _BookAppointmentScreenState
    extends ConsumerState<BookAppointmentScreen> {
  final _reasonController = TextEditingController();
  bool _bookingSuccessHandled = false;

  @override
  void initState() {
    super.initState();
    // Reset state khi màn hình được mở lại
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(bookAppointmentViewModelProvider.notifier).resetScreenState();
      _bookingSuccessHandled = false;
    });
  }

  @override
  void dispose() {
    _reasonController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(bookAppointmentViewModelProvider);

    // Dùng ref.listen để xử lý booking success
    // Chỉ trigger khi prev = null và next != null (chuyển từ chưa success sang success)
    ref.listen<BookAppointmentState>(bookAppointmentViewModelProvider, (prev, next) {
      if (prev?.bookingSuccess == null && next.bookingSuccess != null) {
        if (!mounted) return;
        _bookingSuccessHandled = true;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Đặt lịch thành công. Bạn sẽ nhận nhắc nhở trước giờ khám.'),
            backgroundColor: AppColors.teal,
            duration: Duration(seconds: 3),
          ),
        );
        // Pop về home
        Navigator.of(context).pop();
      }
    });

    return Scaffold(
      appBar: AppBar(title: const Text('Đặt lịch khám')),
      body: SafeArea(
        child: _buildBody(state),
      ),
    );
  }

  Widget _buildBody(BookAppointmentState state) {
    if (state.isLoading && state.slots.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }
    if (state.errorMessage != null && state.slots.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                state.errorMessage!,
                textAlign: TextAlign.center,
                style: const TextStyle(color: AppColors.danger),
              ),
              const SizedBox(height: 16),
              ElevatedButton(
                onPressed: () => ref
                    .read(bookAppointmentViewModelProvider.notifier)
                    .loadSlots(),
                child: const Text('THỬ LẠI'),
              ),
            ],
          ),
        ),
      );
    }

    return RefreshIndicator(
      onRefresh: () => ref
          .read(bookAppointmentViewModelProvider.notifier)
          .loadSlots(),
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 32),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (state.errorMessage != null) ...[
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: const Color(0xFFFBEAE9),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Text(
                  state.errorMessage!,
                  style: const TextStyle(color: AppColors.danger, fontSize: 13),
                ),
              ),
              const SizedBox(height: 16),
            ],
            if (state.slots.isEmpty)
              Container(
                padding: const EdgeInsets.all(24),
                decoration: BoxDecoration(
                  color: Colors.white,
                  border: Border.all(color: AppColors.border),
                  borderRadius: BorderRadius.circular(14),
                ),
                child: const Text(
                  'Hiện chưa có khung giờ mở nào. Vui lòng quay lại sau.',
                  textAlign: TextAlign.center,
                  style: TextStyle(color: AppColors.muted),
                ),
              )
            else ...[
              // Luôn hiện filter bác sĩ
              if (state.doctorOptions.isNotEmpty) _doctorSection(state),
              const SizedBox(height: 20),
              // Toggle hiển thị tuần / tất cả
              _viewToggleSection(state),
              const SizedBox(height: 20),
              if (state.availableDates.isNotEmpty) _dateSection(state),
              const SizedBox(height: 20),
              _slotsSection(state),
              const SizedBox(height: 20),
              _reasonSection(),
              const SizedBox(height: 24),
              _confirmButton(state),
            ],
          ],
        ),
      ),
    );
  }

  /// Toggle hiển thị theo tuần (Tuần này hoặc Tuần sau)
  Widget _viewToggleSection(BookAppointmentState state) {
    final thisWeekLabel = 'Tuần này (${_getThisWeekLabel()})';
    final nextWeekLabel = 'Tuần sau (${_getNextWeekLabel()})';

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _sectionLabel('XEM THEO TUẦN'),
        Row(
          children: [
            Expanded(
              child: _toggleButton(
                label: thisWeekLabel,
                selected: state.showWeekView,
                onTap: () {
                  if (!state.showWeekView) {
                    ref.read(bookAppointmentViewModelProvider.notifier).toggleWeekView();
                  }
                },
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: _toggleButton(
                label: nextWeekLabel,
                selected: !state.showWeekView,
                onTap: () {
                  if (state.showWeekView) {
                    ref.read(bookAppointmentViewModelProvider.notifier).toggleWeekView();
                  }
                },
              ),
            ),
          ],
        ),
      ],
    );
  }

  Widget _toggleButton({
    required String label,
    required bool selected,
    required VoidCallback onTap,
  }) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(10),
      child: Container(
        padding: const EdgeInsets.symmetric(vertical: 12),
        decoration: BoxDecoration(
          color: selected ? AppColors.teal : Colors.white,
          border: Border.all(color: selected ? AppColors.teal : AppColors.border),
          borderRadius: BorderRadius.circular(10),
        ),
        child: Text(
          label,
          textAlign: TextAlign.center,
          style: TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w600,
            color: selected ? Colors.white : AppColors.navy,
          ),
        ),
      ),
    );
  }

  Widget _doctorSection(BookAppointmentState state) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _sectionLabel('BÁC SĨ PHỤ TRÁCH'),
        DropdownSearch<String>(
          popupProps: PopupProps.menu(
            showSearchBox: true,
            searchFieldProps: TextFieldProps(
              decoration: const InputDecoration(
                hintText: 'Tìm kiếm bác sĩ...',
                prefixIcon: Icon(Icons.search),
              ),
            ),
            fit: FlexFit.loose,
          ),
          items: state.doctorOptions.map((d) => d.name).toList(),
          selectedItem: state.selectedDoctorId != null
              ? state.doctorOptions
                  .firstWhere(
                    (d) => d.id == state.selectedDoctorId,
                    orElse: () => state.doctorOptions.first,
                  )
                  .name
              : null,
          onChanged: (name) {
            if (name == null) return;
            final doctor = state.doctorOptions.firstWhere((d) => d.name == name);
            ref.read(bookAppointmentViewModelProvider.notifier).selectDoctor(doctor.id);
          },
          dropdownDecoratorProps: DropDownDecoratorProps(
            dropdownSearchDecoration: const InputDecoration(
              prefixIcon: Icon(Icons.person_outline),
              hintText: 'Chọn bác sĩ',
            ),
          ),
        ),
      ],
    );
  }

  Widget _dateSection(BookAppointmentState state) {
    // Lấy danh sách ngày hiển thị dựa trên chế độ xem
    final displayDates = _getDisplayDates(state);
    final weekRange = _getWeekRangeLabel();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _sectionLabel(state.showWeekView
            ? 'TUẦN NÀY ($weekRange)'
            : 'TUẦN SAU ($weekRange)'),
        SizedBox(
          height: 56,
          child: ListView.separated(
            scrollDirection: Axis.horizontal,
            itemCount: displayDates.length,
            separatorBuilder: (_, __) => const SizedBox(width: 8),
            itemBuilder: (context, index) {
              final d = displayDates[index];
              return _dateChip(
                label: _formatDate(d),
                selected: state.selectedDate != null &&
                    _isSameDay(state.selectedDate!, d),
                onTap: () => ref
                    .read(bookAppointmentViewModelProvider.notifier)
                    .selectDate(d),
              );
            },
          ),
        ),
      ],
    );
  }

  /// Lấy danh sách ngày hiển thị dựa trên chế độ xem.
  /// - Tuần này: ngày trong tuần hiện tại (T2 - CN)
  /// - Tuần sau: ngày trong tuần tiếp theo (T2 - CN)
  List<DateTime> _getDisplayDates(BookAppointmentState state) {
    final now = DateTime.now();
    final currentMonday = DateTime(now.year, now.month, now.day)
        .subtract(Duration(days: now.weekday - 1));

    if (state.showWeekView) {
      // Tuần này: T2 đến CN của tuần hiện tại
      final thisWeekEnd = currentMonday.add(const Duration(days: 6));
      return state.availableDates.where((d) {
        return !d.isBefore(currentMonday) && !d.isAfter(thisWeekEnd);
      }).toList();
    } else {
      // Tuần sau: T2 đến CN của tuần tiếp theo
      final nextMonday = currentMonday.add(const Duration(days: 7));
      final nextSunday = nextMonday.add(const Duration(days: 6));
      return state.availableDates.where((d) {
        return !d.isBefore(nextMonday) && !d.isAfter(nextSunday);
      }).toList();
    }
  }

  Widget _dateChip({
    required String label,
    required bool selected,
    required VoidCallback onTap,
  }) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(28),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        decoration: BoxDecoration(
          color: selected ? AppColors.teal : Colors.white,
          border: Border.all(
            color: selected ? AppColors.teal : AppColors.border,
          ),
          borderRadius: BorderRadius.circular(28),
        ),
        child: Text(
          label,
          style: TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w600,
            color: selected ? Colors.white : AppColors.navy,
          ),
        ),
      ),
    );
  }

  Widget _slotsSection(BookAppointmentState state) {
    // LUÔN cần chọn ngày mới hiện slots (theo yêu cầu mới)
    final needsDateSelection = state.selectedDate == null;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _sectionLabel('Khung giờ khả dụng'),
        if (needsDateSelection)
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: const Color(0xFFFFF8E1), // Màu vàng nhạt
              border: Border.all(color: const Color(0xFFFFB300)),
              borderRadius: BorderRadius.circular(10),
            ),
            child: const Row(
              children: [
                Icon(Icons.info_outline, color: Color(0xFFE65100), size: 20),
                SizedBox(width: 8),
                Expanded(
                  child: Text(
                    'Vui lòng chọn ngày khám để xem các khung giờ.',
                    style: TextStyle(color: Color(0xFFE65100), fontSize: 13),
                  ),
                ),
              ],
            ),
          )
        else if (state.visibleSlots.isEmpty)
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: Colors.white,
              border: Border.all(color: AppColors.border),
              borderRadius: BorderRadius.circular(10),
            ),
            child: const Text(
              'Không có khung giờ cho ngày đã chọn.',
              style: TextStyle(color: AppColors.muted, fontSize: 13),
            ),
          )
        else
          GridView.builder(
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
              crossAxisCount: 3,
              mainAxisSpacing: 10,
              crossAxisSpacing: 10,
              childAspectRatio: 1.6,
            ),
            itemCount: state.visibleSlots.length,
            itemBuilder: (context, i) {
              final slot = state.visibleSlots[i];
              return SlotPill(
                label: slot.startTime,
                subLabel: slot.doctorName,
                selected: state.selectedSlotId == slot.id,
                onTap: () => ref
                    .read(bookAppointmentViewModelProvider.notifier)
                    .selectSlot(slot.id),
              );
            },
          ),
      ],
    );
  }

  Widget _reasonSection() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _sectionLabel('LÝ DO KHÁM (TÙY CHỌN)'),
        TextField(
          controller: _reasonController,
          // KHÔNG gọi updateReason ở đây - tránh rebuild toàn bộ screen
          // Text được đọc trực tiếp từ controller khi submit
          maxLines: 3,
          minLines: 2,
          decoration: const InputDecoration(
            hintText: 'Ví dụ: Đau hạ vị 2 ngày nay, rong kinh nhẹ...',
            alignLabelWithHint: true,
          ),
        ),
      ],
    );
  }

  Widget _confirmButton(BookAppointmentState state) {
    final enabled = state.selectedSlotId != null && !state.isBooking;
    return ElevatedButton(
      onPressed: enabled
          ? () => ref.read(bookAppointmentViewModelProvider.notifier).book(
                reason: _reasonController.text.trim(),
              )
          : null,
      child: state.isBooking
          ? const SizedBox(
              height: 20,
              width: 20,
              child: CircularProgressIndicator(
                strokeWidth: 2,
                color: Colors.white,
              ),
            )
          : const Text('XÁC NHẬN ĐẶT LỊCH'),
    );
  }

  Widget _sectionLabel(String text) => Padding(
        padding: const EdgeInsets.only(bottom: 8, left: 4),
        child: Text(
          text,
          style: const TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w700,
            letterSpacing: 1.1,
            color: AppColors.navy,
          ),
        ),
      );

  static String _formatDate(DateTime d) {
    const weekdays = [
      'T2',
      'T3',
      'T4',
      'T5',
      'T6',
      'T7',
      'CN',
    ];
    final wd = weekdays[d.weekday - 1];
    return '$wd (${d.day.toString().padLeft(2, '0')}/${d.month.toString().padLeft(2, '0')})';
  }

  static bool _isSameDay(DateTime a, DateTime b) =>
      a.year == b.year && a.month == b.month && a.day == b.day;

  /// Lấy label cho tuần hiện tại (T2 - CN)
  String _getWeekRangeLabel() {
    final now = DateTime.now();
    final monday = now.subtract(Duration(days: now.weekday - 1));
    final sunday = monday.add(const Duration(days: 6));
    return '${monday.day.toString().padLeft(2, '0')}/${monday.month.toString().padLeft(2, '0')} - '
        '${sunday.day.toString().padLeft(2, '0')}/${sunday.month.toString().padLeft(2, '0')}';
  }

  /// Lấy label cho "Tuần này": ngày T2 đến CN của tuần hiện tại
  static String _getThisWeekLabel() {
    final now = DateTime.now();
    final monday = now.subtract(Duration(days: now.weekday - 1));
    final sunday = monday.add(const Duration(days: 6));
    return '${monday.day.toString().padLeft(2, '0')}/${monday.month.toString().padLeft(2, '0')}-'
        '${sunday.day.toString().padLeft(2, '0')}/${sunday.month.toString().padLeft(2, '0')}';
  }

  /// Lấy label cho "Tuần sau": ngày T2 đến CN của tuần tiếp theo
  static String _getNextWeekLabel() {
    final now = DateTime.now();
    final monday = now.subtract(Duration(days: now.weekday - 1));
    final nextMonday = monday.add(const Duration(days: 7));
    final nextSunday = nextMonday.add(const Duration(days: 6));
    return '${nextMonday.day.toString().padLeft(2, '0')}/${nextMonday.month.toString().padLeft(2, '0')}-'
        '${nextSunday.day.toString().padLeft(2, '0')}/${nextSunday.month.toString().padLeft(2, '0')}';
  }
}
