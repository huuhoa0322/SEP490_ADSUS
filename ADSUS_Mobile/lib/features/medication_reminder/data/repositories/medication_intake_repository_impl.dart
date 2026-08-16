import 'package:dio/dio.dart';

import '../../../../core/network/api_exception.dart';
import '../../domain/entities/intake_log.dart';
import '../../domain/repositories/medication_intake_repository.dart';
import '../dtos/intake_log_dto.dart';

/// Gọi 3 endpoint Module 7 của Patient qua Dio. Token đã được `dio_client.dart`
/// tự gắn ở interceptor (Bearer + SecureStorage). Impl này chỉ lo ép kiểu
/// DTO ↔ entity + translate lỗi sang tiếng Việt.
class MedicationIntakeRepositoryImpl implements MedicationIntakeRepository {
  const MedicationIntakeRepositoryImpl(this._dio);

  final Dio _dio;

  @override
  Future<List<IntakeLog>> getMyIntakeLogs() async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(
        '/api/v1/me/medication-intakes',
      );
      final envelope = IntakeLogEnvelope.fromJson(res.data);
      final raw = envelope.data ?? const [];
      return raw.map(_fromJson).toList(growable: false);
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e, fallback: 'Không tải được lịch uống thuốc.');
    }
  }

  @override
  Future<List<IntakeLog>> getIntakeLogsByPrescription(
    String prescriptionId,
  ) async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(
        '/api/v1/me/medication-intakes/prescription/$prescriptionId',
      );
      final envelope = IntakeLogEnvelope.fromJson(res.data);
      final raw = envelope.data ?? const [];
      return raw.map(_fromJson).toList(growable: false);
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e, fallback: 'Không tải được lịch uống của đơn thuốc.');
    }
  }

  @override
  Future<void> confirmIntake(String intakeId) async {
    try {
      await _dio.post<void>(
        '/api/v1/me/medication-intakes/$intakeId/confirm',
      );
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e, fallback: 'Không ghi nhận được việc uống thuốc.');
    }
  }

  IntakeLog _fromJson(Map<String, dynamic> json) {
    final scheduledRaw = json['scheduledTime'] as String;
    final confirmedRaw = json['confirmedAt'] as String?;
    return IntakeLog(
      intakeId: json['intakeId'] as String,
      prescriptionItemId: json['prescriptionItemId'] as String,
      // Backend .NET serialize DateTime UTC → parse 'Z' suffix.
      scheduledTimeUtc: DateTime.parse(scheduledRaw).toUtc(),
      confirmedAtUtc: confirmedRaw == null ? null : DateTime.parse(confirmedRaw).toUtc(),
      status: IntakeStatus.fromWire(json['status'] as String),
      medicineName: json['medicineName'] as String,
      dosage: json['dosage'] as String,
      instructions: json['instructions'] as String?,
    );
  }
}