import 'package:flutter_test/flutter_test.dart';
import 'package:adsus_mobile/core/constants/api_constants.dart';

void main() {
  group('TC-FE-01: ApiConstants Endpoints for Patient Relationships', () {
    test('patientRelationships endpoint should be /api/v1/relatives', () {
      expect(ApiConstants.patientRelationships, '/api/v1/relatives');
    });

    test('deletePatientRelationship endpoint should format /api/v1/relatives/:id', () {
      const id = 'test-rel-123';
      expect(ApiConstants.deletePatientRelationship(id), '/api/v1/relatives/test-rel-123');
    });

    test('checkPhoneRegistered endpoint should be /api/v1/relatives/check-phone', () {
      expect(ApiConstants.checkPhoneRegistered, '/api/v1/relatives/check-phone');
    });
  });
}
