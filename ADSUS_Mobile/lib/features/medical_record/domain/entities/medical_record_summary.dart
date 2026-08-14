import 'medical_record_case.dart';

/// Phiên bản rút gọn của [MedicalRecordCase] — dùng cho danh sách (SCR-13).
///
/// Backend trả về cho GET /cases/me (API Spec #25) — CHỈ 4 field này, không có tên bác
/// sĩ, không có kết luận rút gọn.
class MedicalRecordSummary {
  const MedicalRecordSummary({
    required this.caseId,
    required this.visitDate,
    required this.status,
    required this.doctorId,
  });

  final String caseId;
  final DateTime visitDate;
  final CaseStatus status;
  final String doctorId;
}
