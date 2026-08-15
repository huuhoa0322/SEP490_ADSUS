import 'package:dio/dio.dart';

import '../../../../core/constants/api_constants.dart';
import '../../../../core/network/api_envelope.dart';
import '../../../../core/network/api_exception.dart';
import '../../domain/entities/medical_record_case.dart';
import '../../domain/entities/medical_record_summary.dart';
import '../../domain/repositories/medical_record_repository.dart';
import '../dtos/case_dtos.dart';
import '../mappers/medical_record_mapper.dart';

/// Triển khai gọi API thật cho UC-08 (Mobile). Chỉ có duy nhất chỗ này được
/// `import 'package:dio/dio.dart'` trong cả module — domain và presentation tầng trên
/// hoàn toàn không biết `dio` tồn tại.
class MedicalRecordRepositoryImpl implements MedicalRecordRepository {
  MedicalRecordRepositoryImpl(this._dio);

  final Dio _dio;

  @override
  Future<List<MedicalRecordSummary>> getMyRecords({
    int page = 1,
    int pageSize = 20,
  }) async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(
        ApiConstants.myCases,
        queryParameters: {'page': page, 'pageSize': pageSize},
      );

      final envelope = ApiEnvelope.fromJson(res.data ?? const {});
      if (envelope.data == null) {
        throw const ApiException('Không tải được danh sách lượt khám.');
      }

      final paged = PagedResultDto.fromJson(
        envelope.data as Map<String, dynamic>,
        CaseSummaryDto.fromJson,
      );
      return paged.items.map(MedicalRecordMapper.summaryFromDto).toList();
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e, fallback: 'Không tải được danh sách lượt khám.');
    } on ApiException {
      rethrow;
    } catch (e) {
      // Lỗi parse (status enum lạ, DateTime.parse hỏng, thiếu field bắt buộc) không phải
      // DioException — nếu không bắt ở đây, ViewModel's `on ApiException catch` cũng không
      // bắt được, màn hình treo loading vĩnh viễn không có cách nào thoát (không cả "Thử lại").
      throw const ApiException('Không tải được danh sách lượt khám.');
    }
  }

  @override
  Future<MedicalRecordCase> getRecordDetail(String caseId) async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(
        ApiConstants.caseDetail(caseId),
      );

      final envelope = ApiEnvelope.fromJson(res.data ?? const {});
      if (envelope.data == null) {
        throw const ApiException('Không tải được chi tiết lượt khám.');
      }

      return MedicalRecordMapper.caseFromDto(
        CaseDto.fromJson(envelope.data as Map<String, dynamic>),
      );
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e, fallback: 'Không tải được chi tiết lượt khám.');
    } on ApiException {
      rethrow;
    } catch (e) {
      throw const ApiException('Không tải được chi tiết lượt khám.');
    }
  }
}
