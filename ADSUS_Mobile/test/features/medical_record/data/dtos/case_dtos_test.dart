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
      // Khoa lai bug Critical da fix 15/08/2026: backend tra 'doctorConclusion', khong phai
      // 'conclusion' - truoc do CaseDto doc sai key nen field nay luon null trong production.
      final dto = CaseDto.fromJson({
        'caseId': 'case-1',
        'visitDate': '2026-07-22',
        'status': 'CONFIRMED',
        'doctorId': 'doctor-1',
        'doctorName': 'BS. Le Minh Hoang',
        'finalDiagnosis': 'U tuyen xo vu phai',
        'doctorConclusion': 'Theo doi dinh ky sau 6 thang',
        'prescription': {'prescriptionId': 'rx-1', 'status': 'ACTIVE'},
        'ultrasoundImages': [],
      });

      expect(dto.doctorName, 'BS. Le Minh Hoang');
      expect(dto.finalDiagnosis, 'U tuyen xo vu phai');
      expect(dto.doctorConclusion, 'Theo doi dinh ky sau 6 thang');
      expect(dto.prescription?.prescriptionId, 'rx-1');
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
