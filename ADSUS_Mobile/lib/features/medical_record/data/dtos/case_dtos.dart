/// DTO khớp 1:1 `CaseSummaryResponse` — API Spec #25 (GET /cases/me).
class CaseSummaryDto {
  const CaseSummaryDto({
    required this.caseId,
    required this.visitDate,
    required this.status,
    required this.doctorId,
  });

  final String caseId;
  final String visitDate;
  final String status;
  final String doctorId;

  factory CaseSummaryDto.fromJson(Map<String, dynamic> json) => CaseSummaryDto(
        caseId: json['caseId'] as String,
        visitDate: json['visitDate'] as String,
        status: json['status'] as String,
        doctorId: json['doctorId'] as String,
      );
}

/// DTO khớp 1:1 `PrescriptionSummary` lồng trong `CaseResponse` — API Spec #23.
class PrescriptionSummaryDto {
  const PrescriptionSummaryDto({required this.prescriptionId, required this.status});

  final String prescriptionId;
  final String status;

  factory PrescriptionSummaryDto.fromJson(Map<String, dynamic> json) =>
      PrescriptionSummaryDto(
        prescriptionId: json['prescriptionId'] as String,
        status: json['status'] as String,
      );
}

/// DTO khớp 1:1 `UltrasoundImageResponse` lồng trong `CaseResponse`/`PatientCaseResponse`.
class UltrasoundImageDto {
  const UltrasoundImageDto({
    required this.imageId,
    required this.uploadedAt,
    this.imageUrl,
    this.note,
  });

  final String imageId;
  final String uploadedAt;
  final String? imageUrl;
  final String? note;

  factory UltrasoundImageDto.fromJson(Map<String, dynamic> json) => UltrasoundImageDto(
        imageId: json['imageId'] as String,
        uploadedAt: json['uploadedAt'] as String,
        imageUrl: json['imageUrl'] as String?,
        note: json['note'] as String?,
      );
}

/// DTO khớp 1:1 `PatientCaseResponse` (field-set của Patient) — API Spec #23 (GET /cases/{id}).
///
/// Đính chính 15/08/2026: thêm doctorName/finalDiagnosis/ultrasoundImages (backend đã trả sẵn,
/// Mobile trước đó không đọc); đổi `conclusion` → `doctorConclusion` và SỬA key JSON đọc —
/// backend trả `doctorConclusion`, không phải `conclusion` (bug Critical, field trước đó luôn
/// null trong production dù backend có dữ liệu thật).
class CaseDto {
  const CaseDto({
    required this.caseId,
    required this.visitDate,
    required this.status,
    required this.doctorId,
    required this.doctorName,
    this.finalDiagnosis,
    this.doctorConclusion,
    this.prescription,
    this.ultrasoundImages = const [],
  });

  final String caseId;
  final String visitDate;
  final String status;
  final String doctorId;
  final String doctorName;
  final String? finalDiagnosis;
  final String? doctorConclusion;
  final PrescriptionSummaryDto? prescription;
  final List<UltrasoundImageDto> ultrasoundImages;

  factory CaseDto.fromJson(Map<String, dynamic> json) => CaseDto(
        caseId: json['caseId'] as String,
        visitDate: json['visitDate'] as String,
        status: json['status'] as String,
        doctorId: json['doctorId'] as String,
        doctorName: json['doctorName'] as String,
        finalDiagnosis: json['finalDiagnosis'] as String?,
        doctorConclusion: json['doctorConclusion'] as String?,
        prescription: json['prescription'] == null
            ? null
            : PrescriptionSummaryDto.fromJson(
                json['prescription'] as Map<String, dynamic>,
              ),
        ultrasoundImages: (json['ultrasoundImages'] as List<dynamic>? ?? const [])
            .map((e) => UltrasoundImageDto.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}
