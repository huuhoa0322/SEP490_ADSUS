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

  group('CaseDiagnosisDto.fromJson', () {
    test('parse dung 4 field tu JSON', () {
      final dto = CaseDiagnosisDto.fromJson({
        'diagnosisItemId': 'd-1',
        'diagnosisName': 'U tuyen xo vu phai',
        'isOther': false,
        'note': 'Kich thuoc 2cm',
      });

      expect(dto.diagnosisItemId, 'd-1');
      expect(dto.diagnosisName, 'U tuyen xo vu phai');
      expect(dto.isOther, isFalse);
      expect(dto.note, 'Kich thuoc 2cm');
    });
  });

  group('CaseDto.fromJson', () {
    test('parse dung doctorName, caseDiagnoses, doctorConclusion (fix key, khong con conclusion)', () {
      final dto = CaseDto.fromJson({
        'caseId': 'case-1',
        'visitDate': '2026-07-22',
        'status': 'CONFIRMED',
        'doctorId': 'doctor-1',
        'doctorName': 'BS. Le Minh Hoang',
        'caseDiagnoses': [
          {
            'diagnosisItemId': 'd-1',
            'diagnosisName': 'U tuyen xo vu phai',
            'isOther': false,
            'note': null,
          }
        ],
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
      expect(dto.caseDiagnoses, hasLength(1));
      expect(dto.caseDiagnoses.first.diagnosisItemId, 'd-1');
      expect(dto.caseDiagnoses.first.diagnosisName, 'U tuyen xo vu phai');
      expect(dto.caseDiagnoses.first.isOther, isFalse);
      expect(dto.doctorConclusion, 'Theo doi dinh ky sau 6 thang');
      expect(dto.prescription?.prescriptionId, 'rx-1');
      expect(dto.prescription?.prescribedDate, '2026-08-15');
      expect(dto.prescription?.generalNote, 'Uong sau an');
      expect(dto.prescription?.items, hasLength(1));
      expect(dto.prescription?.items.first.medicineName, 'Paracetamol 500mg');
      expect(dto.prescription?.items.first.dosage, '1 vien/lan, 2 lan/ngay');
      expect(dto.prescription?.items.first.durationDays, 5);
      expect(dto.prescription?.items.first.instructions, 'Uong sau an');
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

    test('co symptoms thi parse dung danh sach trieu chung va ghi chu', () {
      final dto = CaseDto.fromJson({
        'caseId': 'case-1',
        'visitDate': '2026-07-22',
        'status': 'CONFIRMED',
        'doctorId': 'doctor-1',
        'doctorName': 'BS. Le Minh Hoang',
        'symptoms': [
          {
            'categoryId': 'cat-1',
            'categoryName': 'Toàn thân',
            'symptomId': 'sym-1',
            'symptomName': 'Sốt nhẹ',
            'otherNote': null,
          },
          {
            'categoryId': 'cat-2',
            'categoryName': 'Tiêu hóa',
            'symptomId': null,
            'symptomName': null,
            'otherNote': 'Đau quặn bụng sau ăn',
          },
        ],
      });

      expect(dto.symptoms, hasLength(2));
      expect(dto.symptoms[0].categoryId, 'cat-1');
      expect(dto.symptoms[0].categoryName, 'Toàn thân');
      expect(dto.symptoms[0].symptomName, 'Sốt nhẹ');
      expect(dto.symptoms[0].otherNote, isNull);

      expect(dto.symptoms[1].categoryId, 'cat-2');
      expect(dto.symptoms[1].categoryName, 'Tiêu hóa');
      expect(dto.symptoms[1].symptomId, isNull);
      expect(dto.symptoms[1].symptomName, isNull);
      expect(dto.symptoms[1].otherNote, 'Đau quặn bụng sau ăn');
    });

    test('symptoms null hoac thieu trong JSON thi tra ve list rong khong loi', () {
      final dto = CaseDto.fromJson({
        'caseId': 'case-1',
        'visitDate': '2026-07-22',
        'status': 'CONFIRMED',
        'doctorId': 'doctor-1',
        'doctorName': 'BS. Le Minh Hoang',
      });

      expect(dto.symptoms, isEmpty);
    });
  });

  group('CaseSymptomDto.fromJson', () {
    test('parse dung day du cac truong category, symptom va otherNote', () {
      final dto = CaseSymptomDto.fromJson({
        'categoryId': 'cat-10',
        'categoryName': 'Hô hấp',
        'symptomId': 'sym-20',
        'symptomName': 'Ho có đờm',
        'otherNote': 'Ho nhieu ve dem',
      });

      expect(dto.categoryId, 'cat-10');
      expect(dto.categoryName, 'Hô hấp');
      expect(dto.symptomId, 'sym-20');
      expect(dto.symptomName, 'Ho có đờm');
      expect(dto.otherNote, 'Ho nhieu ve dem');
    });
  });
}
