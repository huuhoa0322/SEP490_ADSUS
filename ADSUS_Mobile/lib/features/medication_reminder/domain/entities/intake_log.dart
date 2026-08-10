/// Trạng thái 1 intake log — derive từ ConfirmedAt ở backend.
/// Map từ IntakeLogResponse.Status (string "PENDING" / "TAKEN").
enum IntakeStatus {
  pending,
  taken;

  static IntakeStatus fromWire(String value) =>
      value.toUpperCase() == 'TAKEN' ? IntakeStatus.taken : IntakeStatus.pending;
}

/// 1 lịch uống thuốc cho bệnh nhân (SCR-19).
///
/// Là phần "clean" của IntakeLogResponse từ backend — không có ID lẫn null,
/// vì widget render sẽ dùng các trường không-nullable. Repository impl ép kiểu.
class IntakeLog {
  const IntakeLog({
    required this.intakeId,
    required this.prescriptionItemId,
    required this.scheduledTimeUtc,
    required this.status,
    this.confirmedAtUtc,
  });

  final String intakeId;

  /// FK tới PrescriptionItem — UI dùng để gom nhóm nếu cần, hoặc lazy load chi tiết.
  final String prescriptionItemId;

  /// Backend serialize theo UTC (`DateTime` từ backend .NET).
  final DateTime scheduledTimeUtc;

  final DateTime? confirmedAtUtc;

  final IntakeStatus status;

  IntakeLog copyWith({IntakeStatus? status, DateTime? confirmedAtUtc}) =>
      IntakeLog(
        intakeId: intakeId,
        prescriptionItemId: prescriptionItemId,
        scheduledTimeUtc: scheduledTimeUtc,
        confirmedAtUtc: confirmedAtUtc ?? this.confirmedAtUtc,
        status: status ?? this.status,
      );

  /// True nếu intake log đã tới giờ uống (scheduledTime <= now) — dùng để bật nút "Đã uống".
  /// Backend đã validate (xem CLAUDE.md §22.2 fix #7) nhưng UI vẫn nên ẩn nút nếu quá sớm,
  /// tránh user bấm rồi nhận 400 từ server.
  bool isReady(DateTime nowUtc) =>
      !scheduledTimeUtc.isAfter(nowUtc) && status == IntakeStatus.pending;
}