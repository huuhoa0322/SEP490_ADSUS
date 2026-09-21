import 'package:flutter_test/flutter_test.dart';
import 'package:adsus_mobile/features/health_log/data/dtos/health_log_dto.dart';
import 'package:adsus_mobile/features/health_log/data/dtos/health_log_request.dart';
import 'package:adsus_mobile/features/health_log/domain/entities/health_log.dart';

void main() {
  group('HealthLogType Enum Tests', () {
    test('fromString parses EXERCISE and DIET case-insensitively', () {
      expect(HealthLogType.fromString('EXERCISE'), HealthLogType.exercise);
      expect(HealthLogType.fromString('exercise'), HealthLogType.exercise);
      expect(HealthLogType.fromString('DIET'), HealthLogType.diet);
      expect(HealthLogType.fromString('diet'), HealthLogType.diet);
    });

    test('fromString throws ArgumentError on unknown type', () {
      expect(() => HealthLogType.fromString('MEDICATION'), throwsArgumentError);
      expect(() => HealthLogType.fromString(''), throwsArgumentError);
    });

    test('value returns uppercase string representation for API', () {
      expect(HealthLogType.exercise.value, 'EXERCISE');
      expect(HealthLogType.diet.value, 'DIET');
    });
  });

  group('HealthLogRequest Tests', () {
    test('toJson formats payload with truncated yyyy-MM-dd date', () {
      final req = HealthLogRequest(
        type: HealthLogType.exercise,
        content: 'Đi bộ 30 phút buổi sáng',
        logDate: DateTime(2026, 9, 18, 14, 30),
      );

      final json = req.toJson();
      expect(json['type'], 'EXERCISE');
      expect(json['content'], 'Đi bộ 30 phút buổi sáng');
      expect(json['logDate'], '2026-09-18');
    });
  });

  group('HealthLogDto & Envelope Tests', () {
    test('HealthLogDto.fromJson parses valid json and converts to entity', () {
      final json = {
        'healthLogId': 'hl-101',
        'patientProfileId': 'prof-202',
        'logDate': '2026-09-18',
        'type': 'DIET',
        'content': 'Ăn sáng yến mạch và sữa hạt',
        'createdAt': '2026-09-18T07:30:00.000Z',
      };

      final dto = HealthLogDto.fromJson(json);
      expect(dto.healthLogId, 'hl-101');
      expect(dto.patientProfileId, 'prof-202');
      expect(dto.type, 'DIET');
      expect(dto.content, 'Ăn sáng yến mạch và sữa hạt');

      final entity = dto.toEntity();
      expect(entity.healthLogId, 'hl-101');
      expect(entity.type, HealthLogType.diet);
      expect(entity.content, 'Ăn sáng yến mạch và sữa hạt');
      expect(entity.logDate.year, 2026);
      expect(entity.logDate.month, 9);
      expect(entity.logDate.day, 18);
    });

    test('HealthLogEnvelope.fromJson parses envelope and handles null', () {
      final json = {
        'message': 'Thành công',
        'data': [
          {
            'healthLogId': 'hl-1',
            'patientProfileId': 'prof-1',
            'logDate': '2026-09-18',
            'type': 'EXERCISE',
            'content': 'Yoga bầu 20 phút',
            'createdAt': '2026-09-18T08:00:00.000Z',
          }
        ]
      };

      final env = HealthLogEnvelope.fromJson(json);
      expect(env.message, 'Thành công');
      expect(env.data, isNotNull);
      expect(env.data!.length, 1);

      final nullEnv = HealthLogEnvelope.fromJson(null);
      expect(nullEnv.data, isNull);
      expect(nullEnv.message, isEmpty);
    });
  });
}
