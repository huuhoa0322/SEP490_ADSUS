import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../../../../core/network/api_exception.dart';
import '../../domain/entities/health_log.dart';
import '../dtos/health_log_dto.dart';
import '../dtos/health_log_request.dart';

/// Repository triển khai gọi API health-logs cho Patient.
///
/// Token được tự động gắn bởi dio_client.dart interceptor, không cần truyền tay.
class HealthLogRepository {
  const HealthLogRepository(this._dio);

  final Dio _dio;

  /// GET /api/v1/health-logs?date=yyyy-MM-dd
  ///
  /// Trả về danh sách ghi chép của 1 ngày. Ngày mặc định là hôm nay.
  Future<List<HealthLog>> getLogs({DateTime? date}) async {
    try {
      final queryParams = <String, dynamic>{};
      if (date != null) {
        final y = date.year.toString();
        final m = date.month.toString().padLeft(2, '0');
        final d = date.day.toString().padLeft(2, '0');
        queryParams['date'] = '$y-$m-$d';
      }

      debugPrint('[HealthLogRepo] GET /api/v1/health-logs params: $queryParams');

      final res = await _dio.get<Map<String, dynamic>>(
        '/api/v1/health-logs',
        queryParameters: queryParams,
      );

      debugPrint('[HealthLogRepo] Response: ${res.data}');

      final envelope = HealthLogEnvelope.fromJson(res.data);
      final raw = envelope.data ?? const [];
      return raw.map((e) => HealthLogDto.fromJson(e).toEntity()).toList();
    } on DioException catch (e) {
      debugPrint('[HealthLogRepo] DioException: ${e.response?.statusCode} - ${e.message}');
      debugPrint('[HealthLogRepo] Response data: ${e.response?.data}');
      throw ApiErrorMapper.general(e, fallback: 'Không tải được nhật ký sức khỏe.');
    }
  }

  /// POST /api/v1/health-logs
  ///
  /// Tạo mới 1 ghi chép. Backend trả về chính nó với id đã tạo.
  Future<HealthLog> createLog(HealthLogRequest request) async {
    try {
      debugPrint('[HealthLogRepo] POST /api/v1/health-logs body: ${request.toJson()}');

      final res = await _dio.post<Map<String, dynamic>>(
        '/api/v1/health-logs',
        data: request.toJson(),
      );

      debugPrint('[HealthLogRepo] POST Response: ${res.data}');

      final json = res.data?['data'] as Map<String, dynamic>?;
      if (json == null) {
        debugPrint('[HealthLogRepo] Response data is null');
        throw const ApiException('Dữ liệu phản hồi không hợp lệ.');
      }
      return HealthLogDto.fromJson(json).toEntity();
    } on DioException catch (e) {
      debugPrint('[HealthLogRepo] POST DioException: ${e.response?.statusCode} - ${e.message}');
      debugPrint('[HealthLogRepo] POST Response data: ${e.response?.data}');
      throw ApiErrorMapper.general(e, fallback: 'Không lưu được nhật ký sức khỏe.');
    }
  }
}
