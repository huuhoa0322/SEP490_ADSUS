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

/// DTO khớp 1:1 `CaseResponse` (field-set của Patient) — API Spec #23 (GET /cases/{id}).
class CaseDto {
  const CaseDto({
    required this.caseId,
    required this.visitDate,
    required this.status,
    required this.doctorId,
    this.conclusion,
    this.prescription,
  });

  final String caseId;
  final String visitDate;
  final String status;
  final String doctorId;
  final String? conclusion;
  final PrescriptionSummaryDto? prescription;

  factory CaseDto.fromJson(Map<String, dynamic> json) => CaseDto(
        caseId: json['caseId'] as String,
        visitDate: json['visitDate'] as String,
        status: json['status'] as String,
        doctorId: json['doctorId'] as String,
        conclusion: json['conclusion'] as String?,
        prescription: json['prescription'] == null
            ? null
            : PrescriptionSummaryDto.fromJson(
                json['prescription'] as Map<String, dynamic>,
              ),
      );
}
