import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../data/dtos/symptom_dtos.dart';
import '../../domain/entities/schedule_slot.dart' show ScheduleSlot, DoctorStatus;
import '../../domain/entities/symptom.dart' show SymptomCategory;

/// Một block triệu chứng trong UI (tương ứng với 1 category)
class SymptomBlock {
  final String id; // Unique ID cho block này
  final String? selectedCategoryId;
  final Set<String> selectedSymptomIds;
  final String otherNote;

  const SymptomBlock({
    required this.id,
    this.selectedCategoryId,
    this.selectedSymptomIds = const {},
    this.otherNote = '',
  });

  SymptomBlock copyWith({
    String? selectedCategoryId,
    Set<String>? selectedSymptomIds,
    String? otherNote,
  }) {
    return SymptomBlock(
      id: id,
      selectedCategoryId: selectedCategoryId ?? this.selectedCategoryId,
      selectedSymptomIds: selectedSymptomIds ?? this.selectedSymptomIds,
      otherNote: otherNote ?? this.otherNote,
    );
  }

  /// Chuyển thành SymptomInput để gửi lên backend
  List<SymptomInput> toSymptomInputs() {
    final inputs = <SymptomInput>[];
    if (selectedCategoryId == null) return inputs;

    for (final symptomId in selectedSymptomIds) {
      inputs.add(SymptomInput(
        categoryId: selectedCategoryId!,
        symptomId: symptomId,
      ));
    }
    // Thêm "Other" nếu có text
    if (otherNote.isNotEmpty) {
      inputs.add(SymptomInput(
        categoryId: selectedCategoryId!,
        otherNote: otherNote,
      ));
    }
    return inputs;
  }
}

/// Trạng thái màn hình Đặt lịch (SCR-21, UC-13).
///
/// Toàn bộ state để View render chỉ nằm ở đây — View không tự nhớ lựa chọn của user.
class BookAppointmentState {
  const BookAppointmentState({
    this.slots = const [],
    this.doctorOptions = const [],
    this.selectedDoctorId,
    this.selectedSlotId,
    this.selectedDate,
    this.availableDates = const [],
    this.reason = '',
    this.isLoading = false,
    this.isBooking = false,
    this.errorMessage,
    this.bookingSuccess,
    this.showWeekView = true, // Mặc định hiển thị tuần hiện tại
    // Symptoms state
    this.symptomCategories = const [],
    this.symptomBlocks = const [],
    this.isLoadingSymptoms = false,
    this.isSymptomSectionExpanded = false,
  });

  /// Toàn bộ slot Open server trả về (sau khi lọc theo status=OPEN).
  final List<ScheduleSlot> slots;

  /// Danh sách bác sĩ rút gọn từ [slots] — id + tên — để hiển thị dropdown.
  /// Chỉ bao gồm bác sĩ có trạng thái active.
  final List<DoctorOption> doctorOptions;

  /// null = chưa chọn bác sĩ (placeholder "Chọn bác sĩ").
  final String? selectedDoctorId;

  /// null = chưa chọn slot.
  final String? selectedSlotId;

  /// null = "Tất cả ngày" (hoặc đã chọn ngày cụ thể).
  final DateTime? selectedDate;

  /// Rút từ [slots] — chỉ những ngày thật sự có slot (T2-CN, trong giới hạn 5 tuần).
  final List<DateTime> availableDates;

  /// true = chỉ hiện tuần hiện tại (T2-CN)
  /// false = hiện tất cả 5 tuần (tuần này + 4 tuần tiếp)
  final bool showWeekView;

  final String reason;
  final bool isLoading;
  final bool isBooking;
  final String? errorMessage;

  /// Mã đặt lịch thành công để View biết khi nào pop về Home.
  final String? bookingSuccess;

  // --- Symptoms state ---
  final List<SymptomCategory> symptomCategories;
  final List<SymptomBlock> symptomBlocks;
  final bool isLoadingSymptoms;
  final bool isSymptomSectionExpanded;

  BookAppointmentState copyWith({
    List<ScheduleSlot>? slots,
    List<DoctorOption>? doctorOptions,
    String? selectedDoctorId,
    String? selectedSlotId,
    DateTime? selectedDate,
    List<DateTime>? availableDates,
    String? reason,
    bool? isLoading,
    bool? isBooking,
    String? errorMessage,
    String? bookingSuccess,
    bool? showWeekView,
    List<SymptomCategory>? symptomCategories,
    List<SymptomBlock>? symptomBlocks,
    bool? isLoadingSymptoms,
    bool? isSymptomSectionExpanded,
    bool clearError = false,
    bool clearSelection = false,
    bool clearBookingSuccess = false,
  }) {
    return BookAppointmentState(
      slots: slots ?? this.slots,
      doctorOptions: doctorOptions ?? this.doctorOptions,
      selectedDoctorId:
          clearSelection ? null : (selectedDoctorId ?? this.selectedDoctorId),
      selectedSlotId:
          clearSelection ? null : (selectedSlotId ?? this.selectedSlotId),
      selectedDate:
          clearSelection ? null : (selectedDate ?? this.selectedDate),
      availableDates: availableDates ?? this.availableDates,
      reason: reason ?? this.reason,
      isLoading: isLoading ?? this.isLoading,
      isBooking: isBooking ?? this.isBooking,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
      bookingSuccess:
          clearBookingSuccess ? null : (bookingSuccess ?? this.bookingSuccess),
      showWeekView: showWeekView ?? this.showWeekView,
      symptomCategories: symptomCategories ?? this.symptomCategories,
      symptomBlocks: symptomBlocks ?? this.symptomBlocks,
      isLoadingSymptoms: isLoadingSymptoms ?? this.isLoadingSymptoms,
      isSymptomSectionExpanded: isSymptomSectionExpanded ?? this.isSymptomSectionExpanded,
    );
  }

  /// Slot đang được chọn (resolve từ id) hoặc null.
  ScheduleSlot? get selectedSlot {
    if (selectedSlotId == null) return null;
    for (final s in slots) {
      if (s.id == selectedSlotId) return s;
    }
    return null;
  }

  /// Ngày bắt đầu của tuần hiện tại (Thứ 2).
  DateTime get currentWeekStart {
    final now = DateTime.now();
    return DateTime(now.year, now.month, now.day)
        .subtract(Duration(days: now.weekday - 1));
  }

  /// Ngày kết thúc của tuần hiện tại (Chủ nhật).
  DateTime get currentWeekEnd => currentWeekStart.add(const Duration(days: 6));

  /// Ngày bắt đầu của tuần sau.
  DateTime get nextWeekStart => currentWeekStart.add(const Duration(days: 7));

  /// Ngày kết thúc của tuần sau (Chủ nhật).
  DateTime get nextWeekEnd => nextWeekStart.add(const Duration(days: 6));

  /// Giới hạn đặt lịch: tối đa 2 tuần tính từ hôm nay.
  DateTime get maxBookingDate {
    final now = DateTime.now();
    return DateTime(now.year, now.month, now.day).add(const Duration(days: 14));
  }

  /// Slot đã lọc theo bác sĩ + tuần + ngày — danh sách thật sự hiện trong grid.
  /// Chỉ hiện slots KHI đã chọn ngày cụ thể VÀ đã chọn bác sĩ.
  List<ScheduleSlot> get visibleSlots {
    final now = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);

    // BẮT BUỘC phải chọn bác sĩ
    if (selectedDoctorId == null) return [];

    // BẮT BUỘC phải chọn ngày
    if (selectedDate == null) return [];

    return slots.where((s) {
      // Chỉ hiện slot từ hôm nay trở đi
      if (s.slotDate.isBefore(today)) return false;

      // Nếu là slot hôm nay, kiểm tra giờ chưa qua
      if (_isSameDay(s.slotDate, today)) {
        if (!_isSlotTimeValid(s.startTime, now)) return false;
      }

      // Giới hạn 2 tuần
      if (s.slotDate.isAfter(maxBookingDate)) return false;

      // Lọc theo ngày đã chọn
      if (!_isSameDay(s.slotDate, selectedDate!)) {
        return false;
      }

      // Lọc theo bác sĩ đã chọn
      if (s.doctorId != selectedDoctorId) {
        return false;
      }

      return true;
    }).toList();
  }

  /// Kiểm tra giờ slot có hợp lệ (chưa qua giờ hiện tại).
  bool _isSlotTimeValid(String? startTime, DateTime now) {
    if (startTime == null) return true;
    final parts = startTime.split(':');
    if (parts.length < 2) return true;
    final hour = int.tryParse(parts[0]) ?? 0;
    final minute = int.tryParse(parts[1]) ?? 0;
    final slotDateTime = DateTime(now.year, now.month, now.day, hour, minute);
    return slotDateTime.isAfter(now);
  }

  /// Kiểm tra 2 ngày có cùng ngày/tháng/năm.
  static bool _isSameDay(DateTime a, DateTime b) =>
      a.year == b.year && a.month == b.month && a.day == b.day;
}

class DoctorOption {
  const DoctorOption({required this.id, required this.name, this.status = DoctorStatus.active});
  final String id;
  final String name;
  final DoctorStatus status;
}

class BookAppointmentViewModel extends Notifier<BookAppointmentState> {
  @override
  BookAppointmentState build() {
    // Tự nạp danh sách slot ngay khi màn mở — bệnh nhân không cần bấm "Tải lại".
    Future.microtask(loadSlots);
    return const BookAppointmentState(isLoading: true);
  }

  Future<void> loadSlots() async {
    state = state.copyWith(isLoading: true, clearError: true);
    try {
      debugPrint('[DEBUG] loadSlots: calling repository.searchOpenSlots()...');
      final slots = await ref.read(appointmentRepositoryProvider).searchOpenSlots();
      debugPrint('[DEBUG] loadSlots: received ${slots.length} slots');
      debugPrint('[DEBUG] loadSlots: first slot = ${slots.isNotEmpty ? slots.first.doctorName : 'none'}');
      state = state.copyWith(
        slots: slots,
        doctorOptions: _extractDoctors(slots),
        availableDates: _extractDates(slots),
        isLoading: false,
      );
      debugPrint('[DEBUG] loadSlots: state updated, visibleSlots = ${state.visibleSlots.length}');
    } on ApiException catch (e) {
      debugPrint('[DEBUG] loadSlots: ApiException = ${e.message}');
      state = state.copyWith(isLoading: false, errorMessage: e.message);
    } catch (e, st) {
      debugPrint('[DEBUG] loadSlots: UNEXPECTED ERROR = $e');
      debugPrint('[DEBUG] loadSlots: stack = $st');
      state = state.copyWith(isLoading: false, errorMessage: e.toString());
    }
  }

  void selectDoctor(String? doctorId) {
    state = state.copyWith(
      selectedDoctorId: doctorId,
      selectedSlotId: null,
    );
  }

  void selectDate(DateTime? date) {
    state = state.copyWith(
      selectedDate: date,
      selectedSlotId: null,
    );
  }

  void selectSlot(String? slotId) {
    state = state.copyWith(selectedSlotId: slotId);
  }

  void toggleWeekView() {
    state = state.copyWith(
      showWeekView: !state.showWeekView,
      selectedDate: null, // Reset date filter khi toggle
      selectedSlotId: null,
    );
  }

  void updateReason(String value) {
    state = state.copyWith(reason: value);
  }

  void resetForNewBooking() {
    // Tạo state mới với slots/availableDates/doctors giữ nguyên
    // nhưng xóa TẤT CẢ selection, reason, bookingSuccess về mặc định
    state = BookAppointmentState(
      slots: state.slots,
      availableDates: state.availableDates,
      doctorOptions: state.doctorOptions,
      showWeekView: true,
      // Clear mọi selection và success state
      bookingSuccess: null,
      selectedSlotId: null,
      selectedDate: null,
      selectedDoctorId: null,
      reason: '',
    );
  }

  /// Reset toàn bộ state khi mở màn hình mới
  void resetScreenState() {
    state = BookAppointmentState(
      slots: state.slots,
      availableDates: state.availableDates,
      doctorOptions: state.doctorOptions,
      showWeekView: true,
      // Clear mọi selection và success state
      bookingSuccess: null,
      selectedSlotId: null,
      selectedDate: null,
      selectedDoctorId: null,
      reason: '',
    );
    _successShown = false;
  }

  /// Xóa bookingSuccess để ngăn hiển thị lại khi quay lại màn hình.
  void clearBookingSuccess() {
    // Tạo state mới với bookingSuccess = null
    state = BookAppointmentState(
      slots: state.slots,
      availableDates: state.availableDates,
      doctorOptions: state.doctorOptions,
      selectedDoctorId: state.selectedDoctorId,
      selectedSlotId: state.selectedSlotId,
      selectedDate: state.selectedDate,
      reason: state.reason,
      isLoading: state.isLoading,
      isBooking: state.isBooking,
      errorMessage: null,
      bookingSuccess: null,
      showWeekView: state.showWeekView,
    );
  }

  /// Track xem đã show success snackbar chưa (instance-level).
  bool _successShown = false;
  bool get hasShownSuccess => _successShown;
  void markSuccessShown() => _successShown = true;
  void resetSuccessShown() {
    _successShown = false;
  }

  // ============ Symptoms Methods ============

  Future<void> loadSymptomCategories() async {
    if (state.symptomCategories.isNotEmpty) return; // Đã load rồi
    state = state.copyWith(isLoadingSymptoms: true);
    try {
      final categories = await ref.read(symptomRepositoryProvider).getCategories();
      state = state.copyWith(
        symptomCategories: categories,
        isLoadingSymptoms: false,
      );
    } catch (e) {
      debugPrint('[DEBUG] loadSymptomCategories error: $e');
      state = state.copyWith(isLoadingSymptoms: false);
    }
  }

  void toggleSymptomSection() {
    final newExpanded = !state.isSymptomSectionExpanded;
    state = state.copyWith(isSymptomSectionExpanded: newExpanded);
    if (newExpanded && state.symptomCategories.isEmpty) {
      loadSymptomCategories();
    }
  }

  void addSymptomBlock() {
    final newBlock = SymptomBlock(id: DateTime.now().millisecondsSinceEpoch.toString());
    state = state.copyWith(
      symptomBlocks: [...state.symptomBlocks, newBlock],
    );
  }

  void removeSymptomBlock(String blockId) {
    state = state.copyWith(
      symptomBlocks: state.symptomBlocks.where((b) => b.id != blockId).toList(),
    );
  }

  void selectSymptomCategory(String blockId, String categoryId) {
    state = state.copyWith(
      symptomBlocks: state.symptomBlocks.map((b) {
        if (b.id == blockId) {
          return b.copyWith(
            selectedCategoryId: categoryId,
            selectedSymptomIds: {}, // Reset symptoms khi đổi category
            otherNote: '', // Reset other note khi đổi category
          );
        }
        return b;
      }).toList(),
    );
  }

  void toggleSymptom(String blockId, String symptomId) {
    state = state.copyWith(
      symptomBlocks: state.symptomBlocks.map((b) {
        if (b.id == blockId) {
          final newSet = Set<String>.from(b.selectedSymptomIds);
          if (newSet.contains(symptomId)) {
            newSet.remove(symptomId);
          } else {
            newSet.add(symptomId);
          }
          return b.copyWith(selectedSymptomIds: newSet);
        }
        return b;
      }).toList(),
    );
  }

  void updateOtherNote(String blockId, String note) {
    state = state.copyWith(
      symptomBlocks: state.symptomBlocks.map((b) {
        if (b.id == blockId) {
          return b.copyWith(otherNote: note);
        }
        return b;
      }).toList(),
    );
  }

  /// Lấy danh sách category IDs đã được chọn ở các block khác
  Set<String> getUsedCategoryIds(String currentBlockId) {
    return state.symptomBlocks
        .where((b) => b.id != currentBlockId && b.selectedCategoryId != null)
        .map((b) => b.selectedCategoryId!)
        .toSet();
  }

  /// Lấy symptoms từ tất cả blocks
  List<SymptomInput> getAllSymptomInputs() {
    return state.symptomBlocks
        .expand((b) => b.toSymptomInputs())
        .toList();
  }

  // ============ Booking Method ============

  Future<void> book({String? reason}) async {
    final slotId = state.selectedSlotId;
    if (slotId == null) return;
    state = state.copyWith(isBooking: true, clearError: true);
    try {
      // Thu thập symptoms từ các blocks
      final symptoms = getAllSymptomInputs();

      final appointment = await ref
          .read(appointmentRepositoryProvider)
          .bookAppointment(
            scheduleSlotId: slotId,
            reason: reason,
            symptoms: symptoms.isNotEmpty ? symptoms : null,
          );

      // Xóa slot đã đặt khỏi danh sách để không hiện lại
      final updatedSlots = state.slots.where((s) => s.id != slotId).toList();

      state = state.copyWith(
        isBooking: false,
        bookingSuccess: appointment.id,
        slots: updatedSlots,
        availableDates: _extractDates(updatedSlots),
        // Reset selection sau khi đặt thành công
        selectedSlotId: null,
        selectedDate: null,
        // Reset symptoms
        symptomBlocks: [],
        isSymptomSectionExpanded: false,
      );
    } on ApiException catch (e) {
      state = state.copyWith(isBooking: false, errorMessage: e.message);
    }
  }
}

final bookAppointmentViewModelProvider =
    NotifierProvider<BookAppointmentViewModel, BookAppointmentState>(
  BookAppointmentViewModel.new,
);

List<DoctorOption> _extractDoctors(List<ScheduleSlot> slots) {
  final seen = <String, DoctorOption>{};
  for (final s in slots) {
    if (s.doctorId.isEmpty) continue;
    // Chỉ thêm bác sĩ có trạng thái active
    if (s.doctorStatus != DoctorStatus.active) continue;
    seen.putIfAbsent(s.doctorId, () => DoctorOption(
      id: s.doctorId,
      name: s.doctorName,
      status: s.doctorStatus,
    ));
  }
  final list = seen.values.toList()
    ..sort((a, b) => a.name.compareTo(b.name));
  return list;
}

/// Trích xuất danh sách ngày từ slots (T2-CN, từ hôm nay đến 2 tuần).
/// Chỉ lấy ngày từ hôm nay trở đi và tối đa 2 tuần.
List<DateTime> _extractDates(List<ScheduleSlot> slots) {
  final seen = <String, DateTime>{};
  final now = DateTime.now();
  final today = DateTime(now.year, now.month, now.day);
  // Giới hạn 2 tuần = 14 ngày
  final maxDate = today.add(const Duration(days: 14));

  for (final s in slots) {
    // Chỉ lấy ngày T2-CN (weekday 1-7) và trong khoảng hôm nay đến 2 tuần
    if (s.slotDate.weekday < 1) continue;
    if (s.slotDate.isBefore(today)) continue;
    if (s.slotDate.isAfter(maxDate)) continue;

    final key = '${s.slotDate.year}-'
        '${s.slotDate.month.toString().padLeft(2, '0')}-'
        '${s.slotDate.day.toString().padLeft(2, '0')}';
    seen.putIfAbsent(key, () => s.slotDate);
  }
  final list = seen.values.toList()..sort((a, b) => a.compareTo(b));
  return list;
}
