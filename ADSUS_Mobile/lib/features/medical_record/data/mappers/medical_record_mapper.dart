import '../../domain/entities/medical_record_case.dart';
import '../../domain/entities/medical_record_image.dart';
import '../../domain/entities/medical_record_prescription.dart';
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
        doctorName: dto.doctorName,
        finalDiagnosis: dto.finalDiagnosis,
        doctorConclusion: dto.doctorConclusion,
        prescription: dto.prescription == null ? null : _prescriptionFromDto(dto.prescription!),
        images: dto.ultrasoundImages.map(_imageFromDto).toList(),
      );

  static MedicalRecordImage _imageFromDto(UltrasoundImageDto dto) => MedicalRecordImage(
        imageId: dto.imageId,
        uploadedAt: DateTime.parse(dto.uploadedAt),
        imageUrl: dto.imageUrl,
        note: dto.note,
      );

  static MedicalRecordPrescription _prescriptionFromDto(PrescriptionSummaryDto dto) =>
      MedicalRecordPrescription(
        prescriptionId: dto.prescriptionId,
        status: PrescriptionStatus.values.byName(dto.status.toLowerCase()),
        prescribedDate: DateTime.parse(dto.prescribedDate),
        generalNote: dto.generalNote,
        items: dto.items.map(_prescriptionItemFromDto).toList(),
      );

  static MedicalRecordPrescriptionItem _prescriptionItemFromDto(PrescriptionItemDto dto) =>
      MedicalRecordPrescriptionItem(
        medicineName: dto.medicineName,
        dosage: dto.dosage,
        durationDays: dto.durationDays,
        startDate: DateTime.parse(dto.startDate),
        instructions: dto.instructions,
      );
}
