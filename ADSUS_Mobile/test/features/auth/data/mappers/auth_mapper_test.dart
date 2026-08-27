import 'package:adsus_mobile/features/auth/data/dtos/auth_dtos.dart';
import 'package:adsus_mobile/features/auth/domain/entities/auth_session.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('AuthMapper.sessionFromJson', () {
    test('mapsUppercaseApiRoleStringToLowerCamelCaseEnum', () {
      final session = AuthMapper.sessionFromJson({
        'accessToken': 'tok',
        'fullName': 'Nguyen Van A',
        'email': null,
        'role': 'PATIENT',
        'mustChangePassword': false,
      });

      expect(session.role, UserRole.patient);
    });

    test('unknownRoleString_MapsToUnknownEnum', () {
      // Chưa từng có role nào trong hệ thống tên "SOMETHING_NEW" — bảo vệ chống việc backend
      // thêm role mới mà client cũ chưa biết map, thay vì ném exception làm crash app.
      final session = AuthMapper.sessionFromJson({
        'accessToken': 'tok',
        'fullName': 'X',
        'role': 'SOMETHING_NEW',
        'mustChangePassword': false,
      });

      expect(session.role, UserRole.unknown);
    });

    test('missingFields_FallBackToSafeDefaults', () {
      final session = AuthMapper.sessionFromJson(const {});

      expect(session.accessToken, '');
      expect(session.fullName, '');
      expect(session.email, isNull);
      expect(session.role, UserRole.unknown);
      expect(session.mustChangePassword, isFalse);
    });

    test('mustChangePasswordTrue_IsPreserved', () {
      final session = AuthMapper.sessionFromJson({
        'accessToken': 'tok',
        'fullName': 'X',
        'role': 'PATIENT',
        'mustChangePassword': true,
      });

      expect(session.mustChangePassword, isTrue);
    });
  });

  group('AuthMapper.profileFromJson', () {
    test('mapsAllFieldsIncludingRoleEnum', () {
      final profile = AuthMapper.profileFromJson({
        'fullName': 'Nguyen Van A',
        'phoneNumber': '0900000000',
        'email': 'a@example.com',
        'dateOfBirth': '1990-05-20',
        'role': 'PATIENT',
        'biometricEnabled': true,
        'mustChangePassword': true,
      });

      expect(profile.fullName, 'Nguyen Van A');
      expect(profile.phoneNumber, '0900000000');
      expect(profile.email, 'a@example.com');
      expect(profile.dateOfBirth, '1990-05-20');
      expect(profile.role, UserRole.patient);
      expect(profile.biometricEnabled, isTrue);
      expect(profile.mustChangePassword, isTrue);
    });

    test('missingOptionalFields_FallBackToSafeDefaults', () {
      final profile = AuthMapper.profileFromJson(const {});

      expect(profile.fullName, '');
      expect(profile.phoneNumber, '');
      expect(profile.email, isNull);
      expect(profile.dateOfBirth, isNull);
      expect(profile.role, UserRole.unknown);
      expect(profile.biometricEnabled, isFalse);
    });
  });

  group('ApiEnvelope.fromJson', () {
    test('parsesCodeMessageAndData', () {
      final envelope = ApiEnvelope.fromJson({
        'code': 200,
        'message': 'ok',
        'data': {'foo': 'bar'},
      });

      expect(envelope.code, 200);
      expect(envelope.message, 'ok');
      expect(envelope.data, {'foo': 'bar'});
    });

    test('missingData_ReturnsNull', () {
      final envelope = ApiEnvelope.fromJson({'code': 200, 'message': 'ok'});
      expect(envelope.data, isNull);
    });
  });
}
