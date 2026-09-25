import 'package:adsus_mobile/features/medical_record/data/dtos/case_dtos.dart';
import 'package:adsus_mobile/features/medical_record/data/dtos/case_feedback_dto.dart';
import 'package:adsus_mobile/features/medical_record/data/mappers/medical_record_mapper.dart';
import 'package:adsus_mobile/features/medical_record/domain/entities/medical_record_case.dart';
import 'package:adsus_mobile/features/medical_record/domain/entities/medical_record_prescription.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('MedicalRecordMapper.summaryFromDto', () {
    test('map dung tung field, status UPPERCASE sang enum lowerCamelCase', () {
      const dto = CaseSummaryDto(
        caseId: 'case-1',
        visitDate: '2026-07-22',
        status: 'CONFIRMED',
        doctorId: 'doctor-1',
      );

      final entity = MedicalRecordMapper.summaryFromDto(dto);

      expect(entity.caseId, 'case-1');
      expect(entity.visitDate, DateTime.parse('2026-07-22'));
      expect(entity.status, CaseStatus.confirmed);
      expect(entity.doctorId, 'doctor-1');
    });

    test('status END (bac si da ke don) cung map dung, khong throw', () {
      const dto = CaseSummaryDto(
        caseId: 'case-2',
        visitDate: '2026-07-20',
        status: 'END',
        doctorId: 'doctor-1',
      );

      final entity = MedicalRecordMapper.summaryFromDto(dto);

      expect(entity.status, CaseStatus.end);
    });
  });

  group('MedicalRecordMapper.caseFromDto', () {
    test('map du doctorName, caseDiagnoses, doctorConclusion, prescription voi items', () {
      const dto = CaseDto(
        caseId: 'case-1',
        visitDate: '2026-07-22',
        status: 'CONFIRMED',
        doctorId: 'doctor-1',
        doctorName: 'BS. Le Minh Hoang',
        caseDiagnoses: [
          CaseDiagnosisDto(
            diagnosisItemId: 'd-1',
            diagnosisName: 'U tuyen xo vu phai',
            isOther: false,
            note: 'Kich thuoc 2cm',
          ),
        ],
        doctorConclusion: 'Theo doi dinh ky',
        prescription: PrescriptionSummaryDto(
          prescriptionId: 'rx-1',
          status: 'ACTIVE',
          prescribedDate: '2026-08-15',
          generalNote: 'Uong sau an',
          items: [
            PrescriptionItemDto(
              medicineName: 'Paracetamol 500mg',
              dosage: '1 vien/lan, 2 lan/ngay',
              durationDays: 5,
              startDate: '2026-08-15',
              instructions: 'Uong sau an',
            ),
          ],
        ),
      );

      final entity = MedicalRecordMapper.caseFromDto(dto);

      expect(entity.doctorName, 'BS. Le Minh Hoang');
      expect(entity.caseDiagnoses, hasLength(1));
      expect(entity.caseDiagnoses.first.diagnosisItemId, 'd-1');
      expect(entity.caseDiagnoses.first.diagnosisName, 'U tuyen xo vu phai');
      expect(entity.caseDiagnoses.first.isOther, isFalse);
      expect(entity.caseDiagnoses.first.note, 'Kich thuoc 2cm');
      expect(entity.doctorConclusion, 'Theo doi dinh ky');
      expect(entity.prescription?.prescriptionId, 'rx-1');
      expect(entity.prescription?.status, PrescriptionStatus.active);
      expect(entity.prescription?.generalNote, 'Uong sau an');
      expect(entity.prescription?.items, hasLength(1));
      expect(entity.prescription?.items.first.medicineName, 'Paracetamol 500mg');
      expect(entity.prescription?.items.first.dosage, '1 vien/lan, 2 lan/ngay');
      expect(entity.prescription?.items.first.durationDays, 5);
      expect(entity.prescription?.items.first.instructions, 'Uong sau an');
      expect(
        entity.prescription?.items.first.startDate,
        DateTime.parse('2026-08-15'),
      );
    });

    test('khong co prescription thi field la null, khong nem loi', () {
      const dto = CaseDto(
        caseId: 'case-1',
        visitDate: '2026-07-22',
        status: 'CONFIRMED',
        doctorId: 'doctor-1',
        doctorName: 'BS. Le Minh Hoang',
        doctorConclusion: 'Kham dinh ky',
      );

      final entity = MedicalRecordMapper.caseFromDto(dto);

      expect(entity.prescription, isNull);
    });

    test('map dung danh sach anh, giu nguyen thu tu tu Dto', () {
      const dto = CaseDto(
        caseId: 'case-1',
        visitDate: '2026-07-22',
        status: 'END',
        doctorId: 'doctor-1',
        doctorName: 'BS. Le Minh Hoang',
        ultrasoundImages: [
          UltrasoundImageDto(
            imageId: 'img-1',
            uploadedAt: '2026-08-14T10:00:00Z',
            imageUrl: 'https://signed-url.example/anh.png',
            note: 'Ghi chu anh',
          ),
        ],
      );

      final entity = MedicalRecordMapper.caseFromDto(dto);

      expect(entity.images, hasLength(1));
      expect(entity.images.first.imageId, 'img-1');
      expect(entity.images.first.imageUrl, 'https://signed-url.example/anh.png');
      // Moc UTC tu backend duoc doi ve gio may de UI hien thi dung gio dia phuong
      expect(entity.images.first.uploadedAt, DateTime.parse('2026-08-14T10:00:00Z').toLocal());
      expect(entity.images.first.uploadedAt.isUtc, isFalse);
    });

    test('khong co anh thi list rong, khong nem loi', () {
      const dto = CaseDto(
        caseId: 'case-1',
        visitDate: '2026-07-22',
        status: 'END',
        doctorId: 'doctor-1',
        doctorName: 'BS. Le Minh Hoang',
      );

      final entity = MedicalRecordMapper.caseFromDto(dto);

      expect(entity.images, isEmpty);
    });

    test('map dung danh sach symptoms sang CaseSymptomEntity', () {
      const dto = CaseDto(
        caseId: 'case-1',
        visitDate: '2026-07-22',
        status: 'CONFIRMED',
        doctorId: 'doctor-1',
        doctorName: 'BS. Le Minh Hoang',
        symptoms: [
          CaseSymptomDto(
            categoryId: 'cat-1',
            categoryName: 'Toàn thân',
            symptomId: 'sym-1',
            symptomName: 'Sốt nhẹ',
            otherNote: null,
          ),
          CaseSymptomDto(
            categoryId: 'cat-2',
            categoryName: 'Tiêu hóa',
            symptomId: null,
            symptomName: null,
            otherNote: 'Đau quặn bụng sau ăn',
          ),
        ],
      );

      final entity = MedicalRecordMapper.caseFromDto(dto);

      expect(entity.symptoms, hasLength(2));
      expect(entity.symptoms[0].categoryId, 'cat-1');
      expect(entity.symptoms[0].categoryName, 'Toàn thân');
      expect(entity.symptoms[0].symptomName, 'Sốt nhẹ');
      expect(entity.symptoms[0].otherNote, isNull);

      expect(entity.symptoms[1].categoryId, 'cat-2');
      expect(entity.symptoms[1].categoryName, 'Tiêu hóa');
      expect(entity.symptoms[1].symptomId, isNull);
      expect(entity.symptoms[1].symptomName, isNull);
      expect(entity.symptoms[1].otherNote, 'Đau quặn bụng sau ăn');
    });
  });

  group('MedicalRecordMapper.feedbackFromDto', () {
    test('map dung 4 field, submittedAt parse dung DateTime (FT-37)', () {
      const dto = CaseFeedbackDto(
        id: 'feedback-1',
        rating: 5,
        content: 'Bac si rat tan tam',
        submittedAt: '2026-08-20T09:30:00Z',
      );

      final entity = MedicalRecordMapper.feedbackFromDto(dto);

      expect(entity.id, 'feedback-1');
      expect(entity.rating, 5);
      expect(entity.content, 'Bac si rat tan tam');
      expect(entity.submittedAt, DateTime.parse('2026-08-20T09:30:00Z').toLocal());
      expect(entity.submittedAt.isUtc, isFalse);
    });

    test('content null thi entity cung null, khong nem loi', () {
      const dto = CaseFeedbackDto(
        id: 'feedback-1',
        rating: 3,
        submittedAt: '2026-08-20T09:30:00Z',
      );

      final entity = MedicalRecordMapper.feedbackFromDto(dto);

      expect(entity.content, isNull);
    });
  });
}
