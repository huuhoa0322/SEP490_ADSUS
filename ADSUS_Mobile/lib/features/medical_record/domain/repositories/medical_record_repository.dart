import '../entities/medical_record_case.dart';
import '../entities/medical_record_summary.dart';

/// Hợp đồng duy nhất mà ViewModel (Task 3/4) được phép phụ thuộc — UC-08.
/// Không bao giờ phụ thuộc trực tiếp vào MedicalRecordRepositoryImpl hay Dio.
abstract interface class MedicalRecordRepository {
  /// Danh sách lượt khám đã Confirmed của Patient đang đăng nhập (SCR-13, GET /cases/me).
  Future<List<MedicalRecordSummary>> getMyRecords({int page = 1, int pageSize = 20});

  /// Chi tiết 1 lượt khám (SCR-14, GET /cases/{id}) — ném ApiException (404 → not found)
  /// nếu Case không thuộc về Patient này hoặc chưa Confirmed (UC-08 AF-01).
  Future<MedicalRecordCase> getRecordDetail(String caseId);
}
