/// Loai ghi chep suc khoe — map tu HealthLog.Type cua backend.
enum HealthLogType {
  exercise('EXERCISE'),
  diet('DIET');

  const HealthLogType(this.value);
  final String value;

  static HealthLogType fromString(String s) {
    final upper = s.toUpperCase();
    for (final e in HealthLogType.values) {
      if (e.value == upper) return e;
    }
    throw ArgumentError('Gia tri khong hop le: $s');
  }
}

/// Ghi chep suc khoe cua benh nhan (Module 9 — FT-35, FT-40, FT-41).
///
/// Phu hop voi HealthLogResponse tu backend C#.
class HealthLog {
  const HealthLog({
    required this.healthLogId,
    required this.patientProfileId,
    required this.logDate,
    required this.type,
    required this.content,
    required this.createdAt,
  });

  final String healthLogId;
  final String patientProfileId;
  final DateTime logDate;
  final HealthLogType type;
  final String content;
  final DateTime createdAt;
}
