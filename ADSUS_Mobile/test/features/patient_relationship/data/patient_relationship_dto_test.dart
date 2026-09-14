import 'package:flutter_test/flutter_test.dart';
import 'package:adsus_mobile/features/patient_relationship/data/dtos/patient_relationship_dtos.dart';

void main() {
  group('TC-FE-02: PatientRelationshipDTO.fromJson mapping', () {
    test('parses standard backend JSON with patientName and patientPhone', () {
      final json = {
        'relationshipId': 'rel-uuid-001',
        'patientProfileId': 'prof-uuid-001',
        'patientName': 'Nguyễn Thị Vợ',
        'patientPhone': '0901234567',
        'dateOfBirth': '1995-05-20',
        'relationshipName': 'Vợ',
        'createdAt': '2026-09-01T08:00:00.000Z',
      };

      final dto = PatientRelationshipDTO.fromJson(json);

      expect(dto.relationshipId, 'rel-uuid-001');
      expect(dto.patientProfileId, 'prof-uuid-001');
      expect(dto.fullName, 'Nguyễn Thị Vợ');
      expect(dto.phone, '0901234567');
      expect(dto.dateOfBirth, '1995-05-20');
      expect(dto.relationshipName, 'Vợ');
      expect(dto.createdAt, '2026-09-01T08:00:00.000Z');

      final entity = dto.toEntity();
      expect(entity.relationshipId, 'rel-uuid-001');
      expect(entity.patientProfileId, 'prof-uuid-001');
      expect(entity.fullName, 'Nguyễn Thị Vợ');
      expect(entity.phone, '0901234567');
      expect(entity.relationshipName, 'Vợ');
      expect(entity.displayName, 'Vợ - Nguyễn Thị Vợ');
      expect(entity.dateOfBirth?.year, 1995);
      expect(entity.dateOfBirth?.month, 5);
      expect(entity.dateOfBirth?.day, 20);
    });

    test('parses alternative JSON keys (id, patientId, fullName, phone)', () {
      final json = {
        'id': 'rel-uuid-002',
        'patientId': 'prof-uuid-002',
        'fullName': 'Trần Văn Con',
        'phone': '0988776655',
        'dateOfBirth': '2015-10-10',
        'relationshipName': 'Con trai',
      };

      final dto = PatientRelationshipDTO.fromJson(json);

      expect(dto.relationshipId, 'rel-uuid-002');
      expect(dto.patientProfileId, 'prof-uuid-002');
      expect(dto.fullName, 'Trần Văn Con');
      expect(dto.phone, '0988776655');
      expect(dto.relationshipName, 'Con trai');

      final entity = dto.toEntity();
      expect(entity.fullName, 'Trần Văn Con');
      expect(entity.displayName, 'Con trai - Trần Văn Con');
    });

    test('handles elderly relative with null phone and null dateOfBirth', () {
      final json = {
        'relationshipId': 'rel-uuid-003',
        'patientProfileId': 'prof-uuid-003',
        'patientName': 'Bà Ngoại',
        'patientPhone': null,
        'relationshipName': 'Bà Ngoại',
        'dateOfBirth': null,
      };

      final dto = PatientRelationshipDTO.fromJson(json);

      expect(dto.fullName, 'Bà Ngoại');
      expect(dto.phone, isNull);
      expect(dto.dateOfBirth, isNull);

      final entity = dto.toEntity();
      expect(entity.phone, isNull);
      expect(entity.dateOfBirth, isNull);
      expect(entity.age, isNull);
      expect(entity.displayName, 'Bà Ngoại - Bà Ngoại');
    });
  });
}
