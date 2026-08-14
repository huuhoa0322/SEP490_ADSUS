import '../../domain/entities/health_log.dart';

/// DTO cho API response health-logs.
///
/// Backend tra ve ApiResponse chuan voi { code, message, data }.
class HealthLogEnvelope {
  const HealthLogEnvelope({required this.data, required this.message});

  final List<Map<String, dynamic>>? data;
  final String message;

  factory HealthLogEnvelope.fromJson(Map<String, dynamic>? json) {
    if (json == null) {
      return const HealthLogEnvelope(data: null, message: '');
    }
    return HealthLogEnvelope(
      data: (json['data'] as List?)?.cast<Map<String, dynamic>>(),
      message: (json['message'] as String?) ?? '',
    );
  }
}

/// Chuyen tu JSON thanh domain entity.
class HealthLogDto {
  const HealthLogDto({
    required this.healthLogId,
    required this.patientProfileId,
    required this.logDate,
    required this.type,
    required this.content,
    required this.createdAt,
  });

  factory HealthLogDto.fromJson(Map<String, dynamic> json) {
    return HealthLogDto(
      healthLogId: json['healthLogId'] as String,
      patientProfileId: json['patientProfileId'] as String,
      // Backend tra ve 'yyyy-MM-dd', parse thanh DateTime muoi 0:00 UTC.
      logDate: DateTime.parse(json['logDate'] as String),
      type: json['type'] as String,
      content: json['content'] as String,
      createdAt: DateTime.parse(json['createdAt'] as String),
    );
  }

  final String healthLogId;
  final String patientProfileId;
  final DateTime logDate;
  final String type;
  final String content;
  final DateTime createdAt;

  HealthLog toEntity() {
    return HealthLog(
      healthLogId: healthLogId,
      patientProfileId: patientProfileId,
      logDate: logDate,
      type: HealthLogType.fromString(type),
      content: content,
      createdAt: createdAt,
    );
  }
}
