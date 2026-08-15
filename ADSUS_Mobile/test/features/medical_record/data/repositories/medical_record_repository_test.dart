import 'package:adsus_mobile/core/constants/api_constants.dart';
import 'package:adsus_mobile/core/network/api_exception.dart';
import 'package:adsus_mobile/features/medical_record/data/repositories/medical_record_repository_impl.dart';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

class _MockDio extends Mock implements Dio {}

void main() {
  late _MockDio dio;
  late MedicalRecordRepositoryImpl repo;

  Response<Map<String, dynamic>> traVe(dynamic data) => Response(
        requestOptions: RequestOptions(path: '/'),
        statusCode: 200,
        data: {'code': 200, 'message': 'OK', 'data': data},
      );

  setUp(() {
    dio = _MockDio();
    repo = MedicalRecordRepositoryImpl(dio);
  });

  group('getMyRecords', () {
    test('goi dung endpoint /cases/me va map PagedResult sang List<Summary>', () async {
      when(() => dio.get<Map<String, dynamic>>(
            ApiConstants.myCases,
            queryParameters: any(named: 'queryParameters'),
          )).thenAnswer((_) async => traVe({
            'items': [
              {
                'caseId': 'case-1',
                'visitDate': '2026-07-22',
                'status': 'CONFIRMED',
                'doctorId': 'doctor-1',
              },
            ],
            'page': 1,
            'pageSize': 20,
            'totalItems': 1,
            'totalPages': 1,
          }));

      final records = await repo.getMyRecords();

      expect(records, hasLength(1));
      expect(records.first.caseId, 'case-1');
      verify(() => dio.get<Map<String, dynamic>>(
            ApiConstants.myCases,
            queryParameters: any(named: 'queryParameters'),
          )).called(1);
    });

    test('loi mang thi nem ApiException, khong de DioException lot ra ngoai', () async {
      when(() => dio.get<Map<String, dynamic>>(
            ApiConstants.myCases,
            queryParameters: any(named: 'queryParameters'),
          )).thenThrow(DioException(requestOptions: RequestOptions(path: '/')));

      await expectLater(repo.getMyRecords(), throwsA(isA<ApiException>()));
    });

    test('loi parse (khong phai DioException) cung nem ApiException, khong treo loading', () async {
      // Khoa lai bug Important da fix 15/08/2026: 1 status la khong ton tai trong CaseStatus
      // lam CaseStatus.values.byName nem ArgumentError - truoc do loi nay lot qua het 2 lop
      // catch, man hinh treo loading vinh vien khong co nut "Thu lai".
      when(() => dio.get<Map<String, dynamic>>(
            ApiConstants.myCases,
            queryParameters: any(named: 'queryParameters'),
          )).thenAnswer((_) async => traVe({
            'items': [
              {
                'caseId': 'case-1',
                'visitDate': '2026-07-22',
                'status': 'MOT_STATUS_LA_KHONG_TON_TAI',
                'doctorId': 'doctor-1',
              },
            ],
            'page': 1,
            'pageSize': 20,
            'totalItems': 1,
            'totalPages': 1,
          }));

      await expectLater(repo.getMyRecords(), throwsA(isA<ApiException>()));
    });
  });

  group('getRecordDetail', () {
    test('goi dung endpoint /cases/{id} va map du doctorConclusion + prescription + images', () async {
      when(() => dio.get<Map<String, dynamic>>(ApiConstants.caseDetail('case-1')))
          .thenAnswer((_) async => traVe({
                'caseId': 'case-1',
                'visitDate': '2026-07-22',
                'status': 'END',
                'doctorId': 'doctor-1',
                'doctorName': 'BS. Le Minh Hoang',
                'finalDiagnosis': 'U tuyen xo vu phai',
                'doctorConclusion': 'Nhan xo tu cung',
                'prescription': {
                  'prescriptionId': 'rx-1',
                  'status': 'ACTIVE',
                  'prescribedDate': '2026-08-15',
                },
                'ultrasoundImages': [
                  {
                    'imageId': 'img-1',
                    'uploadedAt': '2026-08-14T10:00:00Z',
                    'imageUrl': 'https://signed-url.example/anh.png',
                    'note': null,
                  },
                ],
              }));

      final detail = await repo.getRecordDetail('case-1');

      expect(detail.doctorConclusion, 'Nhan xo tu cung');
      expect(detail.finalDiagnosis, 'U tuyen xo vu phai');
      expect(detail.doctorName, 'BS. Le Minh Hoang');
      expect(detail.prescription?.prescriptionId, 'rx-1');
      expect(detail.images, hasLength(1));
      expect(detail.images.first.imageUrl, 'https://signed-url.example/anh.png');
    });

    test('loi mang thi nem ApiException', () async {
      when(() => dio.get<Map<String, dynamic>>(ApiConstants.caseDetail('case-1')))
          .thenThrow(DioException(requestOptions: RequestOptions(path: '/')));

      await expectLater(repo.getRecordDetail('case-1'), throwsA(isA<ApiException>()));
    });

    test('loi parse (khong phai DioException) cung nem ApiException, khong treo loading', () async {
      when(() => dio.get<Map<String, dynamic>>(ApiConstants.caseDetail('case-1')))
          .thenAnswer((_) async => traVe({
                'caseId': 'case-1',
                'visitDate': 'khong-phai-ngay-hop-le',
                'status': 'END',
                'doctorId': 'doctor-1',
                'doctorName': 'BS. Le Minh Hoang',
              }));

      await expectLater(repo.getRecordDetail('case-1'), throwsA(isA<ApiException>()));
    });
  });
}
