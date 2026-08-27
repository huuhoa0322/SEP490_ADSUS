import '../entities/appointment.dart';
import '../entities/appointment_summary.dart';
import '../entities/schedule_slot.dart';
import '../../data/dtos/symptom_dtos.dart';

/// Hợp đồng cho tầng dữ liệu của module Đặt lịch (UC-13, UC-14).
///
/// Chỉ những endpoint mà vai trò Patient được phép gọi theo
/// `08_Module08_Appointment_Scheduling_API_Spec.md`. Các endpoint Doctor/Nurse
/// (POST/PATCH schedule-slots, GET /schedule-slots/{id}/appointments) không nằm trong
/// hợp đồng này vì mobile chỉ phục vụ bệnh nhân.
abstract interface class AppointmentRepository {
  /// UC-13 bước 2 — liệt kê các khung giờ đang mở để bệnh nhân chọn.
  ///
  /// Tầng data luôn truyền `status=OPEN` (BR-02), nhưng [doctorId] và [slotDate] được
  /// tuỳ chọn vì UC-15 cho phép lọc. Trả về các slot đã sort theo [ScheduleSlot.startAt].
  Future<List<ScheduleSlot>> searchOpenSlots({
    String? doctorId,
    DateTime? slotDate,
  });

  /// UC-13 bước 4-5 — đặt lịch vào một khung đang Open.
  ///
  /// BR-01: nếu bệnh nhân đã có cuộc hẹn Booked trên cùng slot, server trả 409.
  /// BR-02: server chỉ chấp nhận khi slot.status == OPEN (trả 422 nếu không).
  /// Trả về Appointment đã tạo (status = booked).
  ///
  /// Thêm [symptoms] parameter để gửi triệu chứng khi đặt lịch.
  Future<Appointment> bookAppointment({
    required String scheduleSlotId,
    String? reason,
    List<SymptomInput>? symptoms,
  });

  /// UC-14 bước 1 — liệt kê các cuộc hẹn của chính bệnh nhân đang đăng nhập.
  /// Có thể lọc theo trạng thái. Mặc định lấy tất cả.
  Future<List<AppointmentSummary>> listMyAppointments({
    AppointmentStatus? status,
    int page = 1,
    int pageSize = 50,
  });

  /// UC-14 — lấy chi tiết một cuộc hẹn (kèm thông tin slot nếu backend nhúng).
  Future<Appointment> getMyAppointment(String id);

  /// UC-14 bước 4-5 — huỷ cuộc hẹn đang Booked.
  ///
  /// BR-02: `cancellationReason` BẮT BUỘC — không cho phép null/empty.
  /// Trả về Appointment đã cập nhật (status = cancelled, cancelledReason populated).
  Future<Appointment> cancelMyAppointment({
    required String id,
    required String cancellationReason,
  });
}
