import 'package:adsus_mobile/features/medical_record/data/dtos/case_dtos.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('CaseSummaryDto.fromJson', () {
    test('parse dung 4 field tu JSON', () {
      final dto = CaseSummaryDto.fromJson({
        'caseId': 'case-1',
        'visitDate': '2026-07-22',
        'status': 'CONFIRMED',
        'doctorId': 'doctor-1',
      });

      expect(dto.caseId, 'case-1');
      expect(dto.visitDate, '2026-07-22');
      expect(dto.status, 'CONFIRMED');
      expect(dto.doctorId, 'doctor-1');
    });
  });

  group('CaseDto.fromJson', () {
    test('co prescription long thi parse ra PrescriptionSummaryDto', () {
      final dto = CaseDto.fromJson({
        'caseId': 'case-1',
        'visitDate': '2026-07-22',
        'status': 'CONFIRMED',
        'doctorId': 'doctor-1',
        'conclusion': 'Nhan xo tu cung',
        'prescription': {'prescriptionId': 'rx-1', 'status': 'ACTIVE'},
      });

      expect(dto.conclusion, 'Nhan xo tu cung');
      expect(dto.prescription?.prescriptionId, 'rx-1');
      expect(dto.prescription?.status, 'ACTIVE');
    });

    test('prescription null trong JSON thi field cung null, khong nem loi', () {
      final dto = CaseDto.fromJson({
        'caseId': 'case-1',
        'visitDate': '2026-07-22',
        'status': 'CONFIRMED',
        'doctorId': 'doctor-1',
        'conclusion': 'Kham dinh ky',
        'prescription': null,
      });

      expect(dto.prescription, isNull);
    });
  });
}
