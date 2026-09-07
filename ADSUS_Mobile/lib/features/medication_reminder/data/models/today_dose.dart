import '../../domain/entities/intake_log.dart';

/// Minimal dose data stored in SharedPreferences for Android widget rendering.
///
/// Kept intentionally small — widget reads from SharedPreferences without a Flutter
/// engine, so we only store what's needed for display (not full IntakeLog fields).
class TodayDose {
  const TodayDose({
    required this.intakeId,
    required this.medicineName,
    required this.dosage,
    required this.scheduledTimeUtc,
    required this.status,
  });

  final String intakeId;
  final String medicineName;
  final String dosage;
  final DateTime scheduledTimeUtc;
  final IntakeStatus status;

  factory TodayDose.fromIntakeLog(IntakeLog log) {
    return TodayDose(
      intakeId: log.intakeId,
      medicineName: log.medicineName,
      dosage: log.dosage,
      scheduledTimeUtc: log.scheduledTimeUtc,
      status: log.status,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'intakeId': intakeId,
      'medicineName': medicineName,
      'dosage': dosage,
      'scheduledTime': scheduledTimeUtc.toUtc().toIso8601String(),
      'status': status.name.toUpperCase(),
    };
  }

  factory TodayDose.fromJson(Map<String, dynamic> json) {
    return TodayDose(
      intakeId: json['intakeId'] as String,
      medicineName: json['medicineName'] as String,
      dosage: json['dosage'] as String,
      scheduledTimeUtc: DateTime.parse(json['scheduledTime'] as String).toUtc(),
      status: IntakeStatus.fromWire(json['status'] as String),
    );
  }
}
