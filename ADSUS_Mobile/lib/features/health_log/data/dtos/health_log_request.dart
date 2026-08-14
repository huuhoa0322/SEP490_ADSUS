import '../../domain/entities/health_log.dart';

/// Request body khi tao moi 1 health log.
class HealthLogRequest {
  const HealthLogRequest({
    required this.type,
    required this.content,
  });

  final HealthLogType type;
  final String content;

  Map<String, dynamic> toJson() {
    return {
      'type': type.value,
      'content': content,
    };
  }
}
