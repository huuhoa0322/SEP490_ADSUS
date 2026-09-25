import '../../../../core/utils/api_date_time.dart';
import '../../domain/entities/medical_record_case.dart';
import '../../domain/entities/medical_record_feedback.dart';
import '../../domain/entities/medical_record_image.dart';
import '../../domain/entities/medical_record_prescription.dart';
import '../../domain/entities/medical_record_summary.dart';
import '../dtos/case_dtos.dart';
import '../dtos/case_feedback_dto.dart';

/// Chuyển Dto (JSON thô) sang Entity (Dart thuần) — nơi DUY NHẤT map chuỗi UPPERCASE
/// backend (`"CONFIRMED"`) sang enum Dart lowerCamelCase (`CaseStatus.confirmed`).
class MedicalRecordMapper {
  static MedicalRecordSummary summaryFromDto(CaseSummaryDto dto) => MedicalRecordSummary(
        caseId: dto.caseId,
        visitDate: DateTime.parse(dto.visitDate),
        status: CaseStatus.values.byName(dto.status.toLowerCase()),
        doctorId: dto.doctorId,
      );

  static MedicalRecordSummary relativeSummaryFromDto(RelativeCaseSummaryDto dto) =>
      MedicalRecordSummary(
        caseId: dto.caseId,
        visitDate: DateTime.parse(dto.visitDate),
        status: CaseStatus.values.byName(dto.status.toLowerCase()),
        doctorId: dto.doctorId,
        patientName: dto.patientName,
        relationshipName: dto.relationshipName,
      );

  static MedicalRecordCase caseFromDto(CaseDto dto) => MedicalRecordCase(
        caseId: dto.caseId,
        visitDate: DateTime.parse(dto.visitDate),
        status: CaseStatus.values.byName(dto.status.toLowerCase()),
        doctorId: dto.doctorId,
        doctorName: dto.doctorName,
        caseDiagnoses: dto.caseDiagnoses
            .map((d) => CaseDiagnosisEntity(
                  diagnosisItemId: d.diagnosisItemId,
                  diagnosisName: d.diagnosisName,
                  isOther: d.isOther,
                  note: d.note,
                ))
            .toList(),
        doctorConclusion: dto.doctorConclusion,
        prescription: dto.prescription == null ? null : _prescriptionFromDto(dto.prescription!),
        images: dto.ultrasoundImages.map(_imageFromDto).toList(),
        symptoms: dto.symptoms
            .map((s) => CaseSymptomEntity(
                  categoryId: s.categoryId,
                  categoryName: s.categoryName,
                  symptomId: s.symptomId,
                  symptomName: s.symptomName,
                  otherNote: s.otherNote,
                ))
            .toList(),
      );

  static MedicalRecordImage _imageFromDto(UltrasoundImageDto dto) {
    // DEBUG: In ra log để xem URL gốc
    // ignore: avoid_print
    print('DEBUG_IMAGE_URL - Original: ${dto.imageUrl}');

    return MedicalRecordImage(
      imageId: dto.imageId,
      uploadedAt: ApiDateTime.parse(dto.uploadedAt),
      imageUrl: dto.imageUrl,
      note: dto.note,
    );
  }

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

  static MedicalRecordFeedback feedbackFromDto(CaseFeedbackDto dto) => MedicalRecordFeedback(
        id: dto.id,
        rating: dto.rating,
        content: dto.content,
        submittedAt: ApiDateTime.parse(dto.submittedAt),
      );
}
