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
      // Backend GET /cases/me CHI tra ca End (chot 14/08/2026 sau khi trao doi lai, xem
      // thiet ke spec) - test nay khoa lai gia tri THAT SU se nhan duoc trong production.
      // Mapper van xu ly duoc ca CONFIRMED (test o tren) de khong throw neu sau nay co
      // endpoint khac tra ve gia tri do - nhung day moi la case Patient thuc te gap.
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
    test('co prescription long thi map day du prescriptionId/Status', () {
      const dto = CaseDto(
        caseId: 'case-1',
        visitDate: '2026-07-22',
        status: 'CONFIRMED',
        doctorId: 'doctor-1',
        conclusion: 'Nhan xo tu cung',
        prescription: PrescriptionSummaryDto(
          prescriptionId: 'rx-1',
          status: 'ACTIVE',
        ),
      );

      final entity = MedicalRecordMapper.caseFromDto(dto);

      expect(entity.conclusion, 'Nhan xo tu cung');
      expect(entity.prescriptionId, 'rx-1');
      expect(entity.prescriptionStatus, 'ACTIVE');
    });

    test('khong co prescription thi 2 field lien quan la null, khong nem loi', () {
      const dto = CaseDto(
        caseId: 'case-1',
        visitDate: '2026-07-22',
        status: 'CONFIRMED',
        doctorId: 'doctor-1',
        conclusion: 'Kham dinh ky',
      );

      final entity = MedicalRecordMapper.caseFromDto(dto);

      expect(entity.prescriptionId, isNull);
      expect(entity.prescriptionStatus, isNull);
    });
  });
}
