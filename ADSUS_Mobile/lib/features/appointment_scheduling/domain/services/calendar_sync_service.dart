import '../entities/appointment.dart';

/// Ngoại lệ khi đồng bộ lịch thất bại.
class CalendarSyncException implements Exception {
  const CalendarSyncException(this.message);
  final String message;

  @override
  String toString() => message;
}

/// UC-16 — đồng bộ cuộc hẹn vào Calendar hệ thống (spec #54, client-only).
///
/// ADSUS_BE không có endpoint cho việc này — Mobile gọi trực tiếp OS Calendar API
/// thông qua `add_2_calendar`. "One-way, no read-back" theo spec: sau khi mở
/// native Calendar dialog và user xác nhận, app không thể kiểm tra event có tồn tại
/// hay bị xoá. Chỉ lưu cờ local "đã thử add" để UI hiển thị icon phù hợp.
abstract interface class CalendarSyncService {
  /// Thêm cuộc hẹn vào Calendar hệ thống.
  ///
  /// Trả về `true` nếu user đã xác nhận thêm vào Calendar.
  /// Trả về `false` nếu user huỷ hoặc không có Calendar app.
  /// Ném [CalendarSyncException] nếu có lỗi hệ thống (không có quyền, lỗi plugin...).
  Future<bool> addAppointmentToCalendar(Appointment appointment);

  /// Kiểm tra cuộc hẹn đã từng được sync vào Calendar trên thiết bị này chưa.
  Future<bool> hasSynced(String appointmentId);

  /// Xoá cờ sync khi appointment bị huỷ hoặc user muốn sync lại.
  Future<void> clearSyncFlag(String appointmentId);
}
