import 'package:dio/dio.dart';

import '../../../../core/constants/api_constants.dart';
import '../../../../core/network/api_exception.dart';
import '../../domain/repositories/reminder_preference_repository.dart';
import '../dtos/reminder_preference_dto.dart';

/// Gọi GET/PUT /api/v1/me/reminder-preference qua Dio.
/// Token được gắn tự động bởi dio_client interceptor.
class ReminderPreferenceRepositoryImpl implements ReminderPreferenceRepository {
  const ReminderPreferenceRepositoryImpl(this._dio);

  final Dio _dio;

  @override
  Future<ReminderPreferenceData> get() async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(
        ApiConstants.reminderPreference,
      );
      final envelope = ReminderPreferenceEnvelope.fromJson(res.data);
      if (envelope.data == null) {
        throw const ApiException('Không tải được cài đặt nhắc nhở.');
      }
      return envelope.data!;
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e, fallback: 'Không tải được cài đặt nhắc nhở.');
    }
  }

  @override
  Future<ReminderPreferenceData> upsert({
    bool? notifEnabled,
    String? morningTime,
    String? middayTime,
    String? eveningTime,
  }) async {
    try {
      final body = <String, dynamic>{};
      if (notifEnabled != null) body['notifEnabled'] = notifEnabled;
      if (morningTime != null) body['morningTime'] = morningTime;
      if (middayTime != null) body['middayTime'] = middayTime;
      if (eveningTime != null) body['eveningTime'] = eveningTime;

      final res = await _dio.put<Map<String, dynamic>>(
        ApiConstants.reminderPreference,
        data: body,
      );
      final envelope = ReminderPreferenceEnvelope.fromJson(res.data);
      if (envelope.data == null) {
        throw const ApiException('Không lưu được cài đặt nhắc nhở.');
      }
      return envelope.data!;
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e, fallback: 'Không lưu được cài đặt nhắc nhở.');
    }
  }
}
