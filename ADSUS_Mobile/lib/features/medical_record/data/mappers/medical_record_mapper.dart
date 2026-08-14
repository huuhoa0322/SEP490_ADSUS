import '../../domain/entities/medical_record_case.dart';
import '../../domain/entities/medical_record_summary.dart';
import '../dtos/case_dtos.dart';

/// Chuyển Dto (JSON thô) sang Entity (Dart thuần) — nơi DUY NHẤT map chuỗi UPPERCASE
/// backend (`"CONFIRMED"`) sang enum Dart lowerCamelCase (`CaseStatus.confirmed`).
class MedicalRecordMapper {
  static MedicalRecordSummary summaryFromDto(CaseSummaryDto dto) => MedicalRecordSummary(
        caseId: dto.caseId,
        visitDate: DateTime.parse(dto.visitDate),
        status: CaseStatus.values.byName(dto.status.toLowerCase()),
        doctorId: dto.doctorId,
      );

  static MedicalRecordCase caseFromDto(CaseDto dto) => MedicalRecordCase(
        caseId: dto.caseId,
        visitDate: DateTime.parse(dto.visitDate),
        status: CaseStatus.values.byName(dto.status.toLowerCase()),
        doctorId: dto.doctorId,
        conclusion: dto.conclusion,
        prescriptionId: dto.prescription?.prescriptionId,
        prescriptionStatus: dto.prescription?.status,
      );
}
