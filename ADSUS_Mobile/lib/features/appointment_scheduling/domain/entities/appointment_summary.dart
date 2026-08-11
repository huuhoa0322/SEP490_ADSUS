import 'appointment.dart';

/// Phiên bản rút gọn của [Appointment] — chỉ dùng trong list/table view.
///
/// Backend trả về cho `GET /appointments` (UC-14).
/// Đã bao gồm slotDate/startTime/endTime/doctorName từ backend.
class AppointmentSummary {
  const AppointmentSummary({
    required this.id,
    required this.slotId,
    required this.status,
    this.reason,
    this.cancelledReason,
    required this.createdAt,
    this.slotDate,
    this.startTime,
    this.endTime,
    this.doctorId,
    this.doctorName,
  });

  final String id;
  final String slotId;
  final AppointmentStatus status;
  final String? reason;

  /// Lý do hủy khi hủy (UC-14 BR-02).
  final String? cancelledReason;

  final DateTime createdAt;

  /// Thông tin slot lồng vào response
  final DateTime? slotDate;
  final String? startTime;
  final String? endTime;
  final String? doctorId;
  final String? doctorName;

  bool get isBooked => status == AppointmentStatus.booked;
  bool get isCancelled => status == AppointmentStatus.cancelled;

  /// Kiểm tra xem lịch hẹn đã qua chưa (so với giờ hiện tại).
  bool get isExpired {
    if (slotDate == null || startTime == null) return false;
    final parts = startTime!.split(':');
    if (parts.length < 2) return false;
    final hour = int.tryParse(parts[0]) ?? 0;
    final minute = int.tryParse(parts[1]) ?? 0;
    final slotDateTime = DateTime(
      slotDate!.year,
      slotDate!.month,
      slotDate!.day,
      hour,
      minute,
    );
    return slotDateTime.isBefore(DateTime.now());
  }

  /// Tên bác sĩ hiển thị, fallback "—" khi backend không trả về.
  String get doctorDisplayName =>
      doctorName == null ? '—' : 'BS. $doctorName';
}
