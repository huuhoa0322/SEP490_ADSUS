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

/// DTO khớp 1:1 `RelativeCaseSummaryResponse` — API (GET /cases/relatives).
/// Mở rộng CaseSummaryDto với tên bệnh nhân + quan hệ.
class RelativeCaseSummaryDto {
  const RelativeCaseSummaryDto({
    required this.caseId,
    required this.visitDate,
    required this.status,
    required this.doctorId,
    required this.patientName,
    this.relationshipName,
  });

  final String caseId;
  final String visitDate;
  final String status;
  final String doctorId;
  final String patientName;
  final String? relationshipName;

  factory RelativeCaseSummaryDto.fromJson(Map<String, dynamic> json) =>
      RelativeCaseSummaryDto(
        caseId: json['caseId'] as String,
        visitDate: json['visitDate'] as String,
        status: json['status'] as String,
        doctorId: json['doctorId'] as String,
        patientName: json['patientName'] as String? ?? 'Người thân',
        relationshipName: json['relationshipName'] as String?,
      );
}

/// DTO khớp 1:1 `PrescriptionItemSummary` lồng trong `PrescriptionSummaryDto`.
class PrescriptionItemDto {
  const PrescriptionItemDto({
    required this.medicineName,
    required this.dosage,
    required this.durationDays,
    required this.startDate,
    this.instructions,
  });

  final String medicineName;
  final String dosage;
  final int durationDays;
  final String startDate;
  final String? instructions;

  factory PrescriptionItemDto.fromJson(Map<String, dynamic> json) => PrescriptionItemDto(
        medicineName: json['medicineName'] as String,
        dosage: json['dosage'] as String,
        durationDays: json['durationDays'] as int,
        startDate: json['startDate'] as String,
        instructions: json['instructions'] as String?,
      );
}

/// DTO khớp 1:1 `PrescriptionSummary` lồng trong `CaseResponse`/`PatientCaseResponse` — API
/// Spec #23. Đính chính 15/08/2026: thêm prescribedDate/generalNote/items (backend đã trả sẵn,
/// Mobile trước đó không đọc — chỉ có prescriptionId/status).
class PrescriptionSummaryDto {
  const PrescriptionSummaryDto({
    required this.prescriptionId,
    required this.status,
    required this.prescribedDate,
    this.generalNote,
    this.items = const [],
  });

  final String prescriptionId;
  final String status;
  final String prescribedDate;
  final String? generalNote;
  final List<PrescriptionItemDto> items;

  factory PrescriptionSummaryDto.fromJson(Map<String, dynamic> json) =>
      PrescriptionSummaryDto(
        prescriptionId: json['prescriptionId'] as String,
        status: json['status'] as String,
        prescribedDate: json['prescribedDate'] as String,
        generalNote: json['generalNote'] as String?,
        items: (json['items'] as List<dynamic>? ?? const [])
            .map((e) => PrescriptionItemDto.fromJson(e as Map<String, dynamic>))
            .toList(),
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

/// DTO cho chẩn đoán bệnh theo danh mục chuẩn (CaseDiagnosisResponse).
class CaseDiagnosisDto {
  const CaseDiagnosisDto({
    required this.diagnosisItemId,
    required this.diagnosisName,
    required this.isOther,
    this.note,
  });

  final String diagnosisItemId;
  final String diagnosisName;
  final bool isOther;
  final String? note;

  factory CaseDiagnosisDto.fromJson(Map<String, dynamic> json) => CaseDiagnosisDto(
        diagnosisItemId: json['diagnosisItemId'] as String,
        diagnosisName: json['diagnosisName'] as String,
        isOther: json['isOther'] as bool,
        note: json['note'] as String?,
      );
}

/// DTO cho triệu chứng của ca khám (CaseSymptomResponse).
class CaseSymptomDto {
  const CaseSymptomDto({
    required this.categoryId,
    required this.categoryName,
    this.symptomId,
    this.symptomName,
    this.otherNote,
  });

  final String categoryId;
  final String categoryName;
  final String? symptomId;
  final String? symptomName;
  final String? otherNote;

  factory CaseSymptomDto.fromJson(Map<String, dynamic> json) => CaseSymptomDto(
        categoryId: json['categoryId'] as String,
        categoryName: json['categoryName'] as String? ?? '',
        symptomId: json['symptomId'] as String?,
        symptomName: json['symptomName'] as String?,
        otherNote: json['otherNote'] as String?,
      );
}

/// DTO khớp 1:1 `PatientCaseResponse` (field-set của Patient) — API Spec #23 (GET /cases/{id}).
///
/// Đính chính 15/08/2026: thêm doctorName/caseDiagnoses/ultrasoundImages (backend đã trả sẵn,
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
    this.caseDiagnoses = const [],
    this.doctorConclusion,
    this.prescription,
    this.ultrasoundImages = const [],
    this.symptoms = const [],
  });

  final String caseId;
  final String visitDate;
  final String status;
  final String doctorId;
  final String doctorName;
  final List<CaseDiagnosisDto> caseDiagnoses;
  final String? doctorConclusion;
  final PrescriptionSummaryDto? prescription;
  final List<UltrasoundImageDto> ultrasoundImages;
  final List<CaseSymptomDto> symptoms;

  factory CaseDto.fromJson(Map<String, dynamic> json) => CaseDto(
        caseId: json['caseId'] as String,
        visitDate: json['visitDate'] as String,
        status: json['status'] as String,
        doctorId: json['doctorId'] as String,
        doctorName: json['doctorName'] as String,
        caseDiagnoses: (json['caseDiagnoses'] as List<dynamic>? ?? const [])
            .map((e) => CaseDiagnosisDto.fromJson(e as Map<String, dynamic>))
            .toList(),
        doctorConclusion: json['doctorConclusion'] as String?,
        prescription: json['prescription'] == null
            ? null
            : PrescriptionSummaryDto.fromJson(
                json['prescription'] as Map<String, dynamic>,
              ),
        ultrasoundImages: (json['ultrasoundImages'] as List<dynamic>? ?? const [])
            .map((e) => UltrasoundImageDto.fromJson(e as Map<String, dynamic>))
            .toList(),
        symptoms: (json['symptoms'] as List<dynamic>? ?? const [])
            .map((e) => CaseSymptomDto.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}
