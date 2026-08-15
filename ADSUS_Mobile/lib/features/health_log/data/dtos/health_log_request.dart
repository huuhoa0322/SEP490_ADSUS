import '../../domain/entities/health_log.dart';

/// Request body khi tao moi 1 health log.
class HealthLogRequest {
  const HealthLogRequest({
    required this.type,
    required this.content,
    required this.logDate,
  });

  final HealthLogType type;
  final String content;
  final DateTime logDate;

  Map<String, dynamic> toJson() {
    return {
      'type': type.value,
      'content': content,
      'logDate': logDate.toIso8601String().split('T').first,
    };
  }
}
