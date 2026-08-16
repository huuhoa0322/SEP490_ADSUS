/// Trạng thái 1 intake log — derive từ ConfirmedAt + ScheduledTime vs now ở backend (Opt-X).
/// Map từ IntakeLogResponse.Status (string "PENDING" / "TAKEN" / "OVERTIME").
enum IntakeStatus {
  pending,
  taken,
  overtime;

  static IntakeStatus fromWire(String value) {
    switch (value.toUpperCase()) {
      case 'TAKEN':
        return IntakeStatus.taken;
      case 'OVERTIME':
        return IntakeStatus.overtime;
      default:
        return IntakeStatus.pending;
    }
  }
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
    required this.medicineName,
    required this.dosage,
    this.instructions,
  });

  final String intakeId;

  /// FK tới PrescriptionItem — UI dùng để gom nhóm nếu cần, hoặc lazy load chi tiết.
  final String prescriptionItemId;

  /// Backend serialize theo UTC (`DateTime` từ backend .NET).
  final DateTime scheduledTimeUtc;

  final DateTime? confirmedAtUtc;

  final IntakeStatus status;

  /// Enrich từ `PrescriptionItem.Medicine.Name` ở backend — tránh client phải join lại.
  final String medicineName;

  /// Liều dùng (vd "1 viên", "5ml") từ `PrescriptionItem.Dosage`.
  final String dosage;

  /// Hướng dẫn cách dùng (vd "Sau ăn", "Trước ngủ") từ `PrescriptionItem.Instructions`.
  final String? instructions;

  IntakeLog copyWith({IntakeStatus? status, DateTime? confirmedAtUtc}) =>
      IntakeLog(
        intakeId: intakeId,
        prescriptionItemId: prescriptionItemId,
        scheduledTimeUtc: scheduledTimeUtc,
        confirmedAtUtc: confirmedAtUtc ?? this.confirmedAtUtc,
        status: status ?? this.status,
        medicineName: medicineName,
        dosage: dosage,
        instructions: instructions,
      );

  /// True nếu intake log đã tới giờ uống (PENDING hoặc OVERTIME, scheduledTime <= now).
  /// Backend đã validate GB-01 nhưng UI vẫn nên ẩn nút nếu quá sớm,
  /// tránh user bấm rồi nhận 400 từ server.
  bool isReady(DateTime nowUtc) =>
      !scheduledTimeUtc.isAfter(nowUtc) &&
      (status == IntakeStatus.pending || status == IntakeStatus.overtime);
}