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
    test('parse dung doctorName, finalDiagnosis, doctorConclusion (fix key, khong con conclusion)', () {
      final dto = CaseDto.fromJson({
        'caseId': 'case-1',
        'visitDate': '2026-07-22',
        'status': 'CONFIRMED',
        'doctorId': 'doctor-1',
        'doctorName': 'BS. Le Minh Hoang',
        'finalDiagnosis': 'U tuyen xo vu phai',
        'doctorConclusion': 'Theo doi dinh ky sau 6 thang',
        'prescription': {
          'prescriptionId': 'rx-1',
          'status': 'ACTIVE',
          'prescribedDate': '2026-08-15',
          'generalNote': 'Uong sau an',
          'items': [
            {
              'medicineName': 'Paracetamol 500mg',
              'dosage': '1 vien/lan, 2 lan/ngay',
              'durationDays': 5,
              'startDate': '2026-08-15',
              'instructions': 'Uong sau an',
            },
          ],
        },
        'ultrasoundImages': [],
      });

      expect(dto.doctorName, 'BS. Le Minh Hoang');
      expect(dto.finalDiagnosis, 'U tuyen xo vu phai');
      expect(dto.doctorConclusion, 'Theo doi dinh ky sau 6 thang');
      expect(dto.prescription?.prescriptionId, 'rx-1');
      expect(dto.prescription?.prescribedDate, '2026-08-15');
      expect(dto.prescription?.generalNote, 'Uong sau an');
      expect(dto.prescription?.items, hasLength(1));
      expect(dto.prescription?.items.first.medicineName, 'Paracetamol 500mg');
      expect(dto.prescription?.items.first.durationDays, 5);
    });

    test('prescription khong co items thi list rong, khong nem loi', () {
      final dto = CaseDto.fromJson({
        'caseId': 'case-1',
        'visitDate': '2026-07-22',
        'status': 'CONFIRMED',
        'doctorId': 'doctor-1',
        'doctorName': 'BS. Le Minh Hoang',
        'doctorConclusion': 'Kham dinh ky',
        'prescription': {
          'prescriptionId': 'rx-1',
          'status': 'ACTIVE',
          'prescribedDate': '2026-08-15',
          'generalNote': null,
        },
      });

      expect(dto.prescription?.items, isEmpty);
      expect(dto.prescription?.generalNote, isNull);
    });

    test('prescription null trong JSON thi field cung null, khong nem loi', () {
      final dto = CaseDto.fromJson({
        'caseId': 'case-1',
        'visitDate': '2026-07-22',
        'status': 'CONFIRMED',
        'doctorId': 'doctor-1',
        'doctorName': 'BS. Le Minh Hoang',
        'doctorConclusion': 'Kham dinh ky',
        'prescription': null,
      });

      expect(dto.prescription, isNull);
    });

    test('khong co ultrasoundImages trong JSON thi list rong, khong nem loi', () {
      final dto = CaseDto.fromJson({
        'caseId': 'case-1',
        'visitDate': '2026-07-22',
        'status': 'CONFIRMED',
        'doctorId': 'doctor-1',
        'doctorName': 'BS. Le Minh Hoang',
      });

      expect(dto.ultrasoundImages, isEmpty);
    });

    test('co ultrasoundImages thi parse dung tung anh, imageUrl null khong nem loi', () {
      final dto = CaseDto.fromJson({
        'caseId': 'case-1',
        'visitDate': '2026-07-22',
        'status': 'CONFIRMED',
        'doctorId': 'doctor-1',
        'doctorName': 'BS. Le Minh Hoang',
        'ultrasoundImages': [
          {
            'imageId': 'img-1',
            'uploadedAt': '2026-08-14T10:00:00Z',
            'imageUrl': 'https://signed-url.example/anh.png',
            'note': 'Ghi chu anh',
          },
          {
            'imageId': 'img-2',
            'uploadedAt': '2026-08-14T10:05:00Z',
            'imageUrl': null,
            'note': null,
          },
        ],
      });

      expect(dto.ultrasoundImages, hasLength(2));
      expect(dto.ultrasoundImages[0].imageId, 'img-1');
      expect(dto.ultrasoundImages[0].imageUrl, 'https://signed-url.example/anh.png');
      expect(dto.ultrasoundImages[1].imageUrl, isNull);
    });
  });
}
