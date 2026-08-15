import 'package:adsus_mobile/features/medical_record/data/dtos/case_dtos.dart';
import 'package:adsus_mobile/features/medical_record/data/mappers/medical_record_mapper.dart';
import 'package:adsus_mobile/features/medical_record/domain/entities/medical_record_case.dart';
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
    test('map du doctorName, finalDiagnosis, doctorConclusion, prescription', () {
      const dto = CaseDto(
        caseId: 'case-1',
        visitDate: '2026-07-22',
        status: 'CONFIRMED',
        doctorId: 'doctor-1',
        doctorName: 'BS. Le Minh Hoang',
        finalDiagnosis: 'U tuyen xo vu phai',
        doctorConclusion: 'Theo doi dinh ky',
        prescription: PrescriptionSummaryDto(
          prescriptionId: 'rx-1',
          status: 'ACTIVE',
        ),
      );

      final entity = MedicalRecordMapper.caseFromDto(dto);

      expect(entity.doctorName, 'BS. Le Minh Hoang');
      expect(entity.finalDiagnosis, 'U tuyen xo vu phai');
      expect(entity.doctorConclusion, 'Theo doi dinh ky');
      expect(entity.prescriptionId, 'rx-1');
      expect(entity.prescriptionStatus, 'ACTIVE');
    });

    test('khong co prescription thi 2 field lien quan la null, khong nem loi', () {
      const dto = CaseDto(
        caseId: 'case-1',
        visitDate: '2026-07-22',
        status: 'CONFIRMED',
        doctorId: 'doctor-1',
        doctorName: 'BS. Le Minh Hoang',
        doctorConclusion: 'Kham dinh ky',
      );

      final entity = MedicalRecordMapper.caseFromDto(dto);

      expect(entity.prescriptionId, isNull);
      expect(entity.prescriptionStatus, isNull);
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
      expect(entity.images.first.uploadedAt, DateTime.parse('2026-08-14T10:00:00Z'));
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
  });
}
