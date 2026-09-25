import 'medical_record_case.dart';

/// Phiên bản rút gọn của [MedicalRecordCase] — dùng cho danh sách (SCR-13).
///
/// Backend trả về cho GET /cases/me (API Spec #25) — CHỈ 4 field này, không có tên bác
/// sĩ, không có kết luận rút gọn.
///
/// Khi là hồ sơ người thân (GET /cases/relatives), bổ sung thêm [patientName] và
/// [relationshipName] để hiển thị badge trên thẻ.
class MedicalRecordSummary {
  const MedicalRecordSummary({
    required this.caseId,
    required this.visitDate,
    required this.status,
    required this.doctorId,
    this.patientName,
    this.relationshipName,
  });

  final String caseId;
  final DateTime visitDate;
  final CaseStatus status;
  final String doctorId;

  /// Tên bệnh nhân (chỉ có khi là hồ sơ người thân).
  final String? patientName;

  /// Quan hệ với người đặt, vd "Mẹ", "Con gái" (chỉ có khi là hồ sơ người thân).
  final String? relationshipName;
}
