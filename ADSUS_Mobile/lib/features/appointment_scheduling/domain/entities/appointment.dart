/// Trạng thái của một cuộc hẹn.
///
/// Theo UCS BR-03, Appointment KHÔNG có trạng thái "Completed" — chỉ Booked hoặc
/// Cancelled. "Đã khám" hay chưa suy ra từ ngày giờ của slot, không lưu vào DB.
enum AppointmentStatus { booked, cancelled }

/// Một cuộc hẹn khám siêu âm đã được bệnh nhân đặt (UC-13) hoặc đã huỷ (UC-14).
///
/// Các trường về slot (ngày, giờ, bác sĩ) là tuỳ chọn — endpoint `GET /appointments` trả
/// về cùng response shape ở cả UC-13 lẫn UC-14, nên tầng mapper phải chịu lỗi khi backend
/// không nhúng thông tin slot; UI sẽ ẩn các dòng đó thay vì ném exception.
class Appointment {
  const Appointment({
    required this.id,
    required this.slotId,
    required this.patientProfileId,
    required this.status,
    required this.createdAt,
    required this.updatedAt,
    this.reason,
    this.cancelledReason,
    this.slotDate,
    this.startTime,
    this.endTime,
    this.doctorName,
  });

  final String id;
  final String slotId;
  final String patientProfileId;
  final AppointmentStatus status;

  /// Lý do khám khi đặt (UC-13, optional).
  final String? reason;

  /// Lý do hủy khi hủy (UC-14 BR-02, bắt buộc nếu status == cancelled).
  final String? cancelledReason;

  final DateTime createdAt;
  final DateTime updatedAt;

  // --- Thông tin slot lồng vào response (optional, backend có thể bỏ trong tương lai) ---

  final DateTime? slotDate;
  final String? startTime;
  final String? endTime;
  final String? doctorName;

  bool get isBooked => status == AppointmentStatus.booked;
  bool get isCancelled => status == AppointmentStatus.cancelled;

  /// Kiểm tra xem lịch khám đã qua chưa (so với giờ hiện tại).
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
