import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:adsus_mobile/core/constants/api_constants.dart';
import 'package:adsus_mobile/features/patient_relationship/data/repositories/patient_relationship_repository_impl.dart';

class _MockDio extends Mock implements Dio {}

void main() {
  late _MockDio mockDio;
  late PatientRelationshipRepositoryImpl repository;

  setUp(() {
    mockDio = _MockDio();
    repository = PatientRelationshipRepositoryImpl(mockDio);
  });

  group('PatientRelationshipRepositoryImpl Tests', () {
    test('TC-FE-03: Repo_GetRelatives_UnwrapsJson when backend returns relatives array', () async {
      when(() => mockDio.get<Map<String, dynamic>>(ApiConstants.patientRelationships))
          .thenAnswer((_) async => Response(
                requestOptions: RequestOptions(path: ApiConstants.patientRelationships),
                statusCode: 200,
                data: {
                  'code': 200,
                  'message': 'Success',
                  'relatives': [
                    {
                      'relationshipId': 'rel-001',
                      'patientProfileId': 'prof-001',
                      'patientName': 'Nguyễn Thị Vợ',
                      'patientPhone': '0901234567',
                      'relationshipName': 'Vợ',
                      'dateOfBirth': '1995-05-20',
                      'createdAt': '2026-09-01T08:00:00.000Z',
                    },
                    {
                      'relationshipId': 'rel-002',
                      'patientProfileId': 'prof-002',
                      'patientName': 'Bà Ngoại',
                      'patientPhone': null,
                      'relationshipName': 'Bà Ngoại',
                      'dateOfBirth': '1945-01-01',
                      'createdAt': '2026-09-02T08:00:00.000Z',
                    }
                  ],
                },
              ));

      final relatives = await repository.getRelatives();

      expect(relatives, hasLength(2));
      expect(relatives[0].relationshipId, 'rel-001');
      expect(relatives[0].fullName, 'Nguyễn Thị Vợ');
      expect(relatives[0].phone, '0901234567');
      expect(relatives[0].relationshipName, 'Vợ');

      expect(relatives[1].relationshipId, 'rel-002');
      expect(relatives[1].fullName, 'Bà Ngoại');
      expect(relatives[1].phone, isNull);
      expect(relatives[1].relationshipName, 'Bà Ngoại');
    });

    test('TC-FE-03 (variant): Repo_GetRelatives_UnwrapsJson with data wrapper', () async {
      when(() => mockDio.get<Map<String, dynamic>>(ApiConstants.patientRelationships))
          .thenAnswer((_) async => Response(
                requestOptions: RequestOptions(path: ApiConstants.patientRelationships),
                statusCode: 200,
                data: {
                  'code': 200,
                  'message': 'Success',
                  'data': {
                    'relatives': [
                      {
                        'relationshipId': 'rel-003',
                        'patientProfileId': 'prof-003',
                        'fullName': 'Con Gái',
                        'phone': '0911223344',
                        'relationshipName': 'Con',
                      }
                    ]
                  }
                },
              ));

      final relatives = await repository.getRelatives();

      expect(relatives, hasLength(1));
      expect(relatives[0].fullName, 'Con Gái');
    });

    test('TC-FE-04: Repo_GetRelatives_EmptyList returns clean empty list', () async {
      when(() => mockDio.get<Map<String, dynamic>>(ApiConstants.patientRelationships))
          .thenAnswer((_) async => Response(
                requestOptions: RequestOptions(path: ApiConstants.patientRelationships),
                statusCode: 200,
                data: {
                  'code': 200,
                  'message': 'Success',
                  'relatives': [],
                },
              ));

      final relatives = await repository.getRelatives();

      expect(relatives, isEmpty);
    });

    test('TC-FE-05: Repo_AddRelative_WithPhone sends payload and returns created entity', () async {
      Map<String, dynamic>? capturedPayload;

      when(() => mockDio.post<Map<String, dynamic>>(
            ApiConstants.patientRelationships,
            data: any(named: 'data'),
          )).thenAnswer((invocation) async {
        capturedPayload = invocation.namedArguments[const Symbol('data')] as Map<String, dynamic>?;
        return Response(
          requestOptions: RequestOptions(path: ApiConstants.patientRelationships),
          statusCode: 200,
          data: {
            'relationshipId': 'rel-new-1',
            'patientProfileId': 'prof-new-1',
            'patientName': 'Trần Thị Vợ',
            'patientPhone': '0912345678',
            'relationshipName': 'Vợ',
            'createdAt': '2026-09-14T00:00:00.000Z',
          },
        );
      });

      final created = await repository.addRelative(
        fullName: 'Trần Thị Vợ',
        phone: '0912345678',
        relationshipName: 'Vợ',
      );

      expect(capturedPayload, isNotNull);
      expect(capturedPayload!['fullName'], 'Trần Thị Vợ');
      expect(capturedPayload!['phone'], '0912345678');
      expect(capturedPayload!['relationshipName'], 'Vợ');

      expect(created.relationshipId, 'rel-new-1');
      expect(created.fullName, 'Trần Thị Vợ');
      expect(created.phone, '0912345678');
    });

    test('TC-FE-06: Repo_AddRelative_NullPhone sends null phone for elderly and succeeds', () async {
      Map<String, dynamic>? capturedPayload;

      when(() => mockDio.post<Map<String, dynamic>>(
            ApiConstants.patientRelationships,
            data: any(named: 'data'),
          )).thenAnswer((invocation) async {
        capturedPayload = invocation.namedArguments[const Symbol('data')] as Map<String, dynamic>?;
        return Response(
          requestOptions: RequestOptions(path: ApiConstants.patientRelationships),
          statusCode: 200,
          data: {
            'relationshipId': 'rel-elderly-1',
            'patientProfileId': 'prof-elderly-1',
            'patientName': 'Bà Ngoại',
            'patientPhone': null,
            'relationshipName': 'Bà Ngoại',
            'createdAt': '2026-09-14T00:00:00.000Z',
          },
        );
      });

      // Calling with empty phone string indicates elderly without phone
      final created = await repository.addRelative(
        fullName: 'Bà Ngoại',
        phone: '',
        relationshipName: 'Bà Ngoại',
      );

      expect(capturedPayload, isNotNull);
      expect(capturedPayload!['fullName'], 'Bà Ngoại');
      expect(capturedPayload!.containsKey('phone'), isFalse);

      expect(created.relationshipId, 'rel-elderly-1');
      expect(created.fullName, 'Bà Ngoại');
      expect(created.phone, isNull);
    });
  });
}
