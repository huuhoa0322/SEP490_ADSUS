import '../entities/intake_log.dart';

/// Module 7 — Patient xem lịch uống thuốc + xác nhận đã uống (SCR-19, UC-19/20).
///
/// GB-09: Mobile chỉ dành cho Patient. Doctor/Nurse xem từ Web khác file
/// (sprint sau hoặc Module 7 SCR-18 trên adsus-fe).
abstract class MedicationIntakeRepository {
  /// GET /api/v1/me/medication-intakes — danh sách lịch uống của bệnh nhân hiện tại.
  /// Trả về mảng rỗng nếu backend `data` là null.
  Future<List<IntakeLog>> getMyIntakeLogs();

  /// GET /api/v1/me/medication-intakes/prescription/{id} — lịch uống của 1 đơn.
  Future<List<IntakeLog>> getIntakeLogsByPrescription(String prescriptionId);

  /// POST /api/v1/me/medication-intakes/{id}/confirm — xác nhận đã uống.
  ///
  /// Backend idempotent (§22.2 fix #7): confirm 2 lần không double-update.
  /// Backend reject 400 nếu scheduledTime > now (chống gian lận tuân thủ).
  /// DioException sẽ được translate sang ApiException ở tầng impl.
  Future<void> confirmIntake(String intakeId);
}