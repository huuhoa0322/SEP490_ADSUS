/// Trạng thái của một khung giờ khám.
///
/// Map từ enum `slot_status` của backend (UPPERCASE Postgres) sang lowerCamelCase Dart
/// ở tầng Mapper — không bao giờ để chuỗi UPPERCASE lọt qua tầng data.
enum SlotStatus { open, closed }

/// Trạng thái tài khoản bác sĩ.
enum DoctorStatus { active, inactive }

/// Khung giờ khám do Bác sĩ/Điều dưỡng đăng ký (UC-15).
///
/// Bệnh nhân chỉ thấy các khung ở trạng thái [SlotStatus.open] khi đặt lịch (UC-13).
/// Theo quyết định 2026-07-23 của UCS, khung giờ KHÔNG có khái niệm "Full" — chỉ Open
/// hay Closed, và số Appointment trên một khung là không giới hạn.
class ScheduleSlot {
  const ScheduleSlot({
    required this.id,
    required this.doctorId,
    required this.doctorName,
    required this.slotDate,
    required this.startTime,
    required this.endTime,
    required this.status,
    this.doctorStatus = DoctorStatus.active,
  });

  final String id;
  final String doctorId;

  /// Tên bác sĩ phụ trách — backend nhúng vào response của mobile để không phải gọi
  /// thêm API tra cứu khi hiển thị.
  final String doctorName;

  /// Trạng thái tài khoản bác sĩ — dùng để filter bác sĩ active trong dropdown.
  final DoctorStatus doctorStatus;

  /// Ngày khám (chỉ giờ phút bằng 0).
  final DateTime slotDate;

  /// "HH:mm" — lưu chuỗi để tránh lệch múi giờ khi re-format cho UI.
  final String startTime;
  final String endTime;

  final SlotStatus status;

  /// Ghép ngày + giờ bắt đầu thành một DateTime để sắp xếp / so sánh.
  DateTime get startAt {
    final parts = startTime.split(':');
    return DateTime(
      slotDate.year,
      slotDate.month,
      slotDate.day,
      int.parse(parts[0]),
      int.parse(parts[1]),
    );
  }

  /// Tên hiển thị gọn gàng: "BS. Nguyễn Văn An".
  String get doctorDisplayName => 'BS. $doctorName';
}
