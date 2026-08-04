import 'package:adsus_mobile/core/constants/storage_keys.dart';
import 'package:adsus_mobile/core/network/api_exception.dart';
import 'package:adsus_mobile/features/auth/data/repositories/auth_repository_impl.dart';
import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

class _MockDio extends Mock implements Dio {}

class _MockStorage extends Mock implements FlutterSecureStorage {}

/// Kiểm thử tầng dữ liệu của phần xác thực.
///
/// Ba luật được khẳng định ở đây đều là những chỗ đã từng sai:
///   - Chỉ bệnh nhân dùng được ứng dụng di động (UC-01).
///   - Đăng xuất phải xoá sạch, không để lại trạng thái vân tay treo lơ lửng.
///   - Đổi tài khoản trên cùng một máy không được kế thừa trạng thái của người trước.
void main() {
  late _MockDio dio;
  late _MockStorage storage;
  late AuthRepositoryImpl repo;

  /// Response giả theo đúng vỏ { code, message, data } của ADSUS_BE.
  Response<Map<String, dynamic>> traVe(Map<String, dynamic> data) => Response(
        requestOptions: RequestOptions(path: '/'),
        statusCode: 200,
        data: {'code': 200, 'message': 'OK', 'data': data},
      );

  Map<String, dynamic> phienDangNhap(String vaiTro) => {
        'accessToken': 'token-gia',
        'fullName': 'Nguyễn Văn A',
        'email': null,
        'role': vaiTro,
        'mustChangePassword': false,
      };

  void gaDangNhapTraVe(String vaiTro) {
    when(() => dio.post<Map<String, dynamic>>(any(), data: any(named: 'data')))
        .thenAnswer((_) async => traVe(phienDangNhap(vaiTro)));
  }

  setUp(() {
    dio = _MockDio();
    storage = _MockStorage();
    repo = AuthRepositoryImpl(dio, storage);

    when(() => storage.read(key: any(named: 'key'))).thenAnswer((_) async => null);
    when(() => storage.write(key: any(named: 'key'), value: any(named: 'value')))
        .thenAnswer((_) async {});
    when(() => storage.delete(key: any(named: 'key'))).thenAnswer((_) async {});
  });

  group('Chi benh nhan dung duoc ung dung di dong', () {
    test('Bac si dang nhap tren Mobile bi tu choi', () async {
      gaDangNhapTraVe('DOCTOR');

      await expectLater(
        repo.signIn(phoneNumber: '0900000002', password: 'Aa123456@'),
        throwsA(isA<ApiException>()),
      );
    });

    test('Bi tu choi thi KHONG luu token lai tren may', () async {
      // Nếu lưu rồi mới chặn thì token của bác sĩ vẫn nằm trong thiết bị.
      gaDangNhapTraVe('ADMIN');

      try {
        await repo.signIn(phoneNumber: '0900000001', password: 'Aa123456@');
        fail('Le ra phai nem ApiException');
      } on ApiException {
        // Đúng như mong đợi.
      }

      verifyNever(
        () => storage.write(key: StorageKeys.accessToken, value: any(named: 'value')),
      );
      verifyNever(
        () => storage.write(key: StorageKeys.pairedPhone, value: any(named: 'value')),
      );
    });

    test('Benh nhan dang nhap thanh cong thi luu token va ghep doi thiet bi', () async {
      gaDangNhapTraVe('PATIENT');

      await repo.signIn(phoneNumber: '0900000003', password: 'Aa123456@');

      verify(() => storage.write(key: StorageKeys.accessToken, value: 'token-gia'))
          .called(1);
      // UC-02 BR-01 — ghép đôi thiết bị.
      verify(() => storage.write(key: StorageKeys.pairedPhone, value: '0900000003'))
          .called(1);
    });
  });

  group('Doi tai khoan tren cung mot may', () {
    test('Nguoi moi KHONG ke thua trang thai van tay cua nguoi cu', () async {
      // Máy đang ghép đôi với 0900000003 và đã bật vân tay.
      when(() => storage.read(key: StorageKeys.pairedPhone))
          .thenAnswer((_) async => '0900000003');
      gaDangNhapTraVe('PATIENT');

      // Người khác đăng nhập.
      await repo.signIn(phoneNumber: '0900000009', password: 'Aa123456@');

      verify(() => storage.delete(key: StorageKeys.biometricEnabled)).called(1);
    });

    test('Cung mot nguoi dang nhap lai thi giu nguyen cai dat van tay', () async {
      when(() => storage.read(key: StorageKeys.pairedPhone))
          .thenAnswer((_) async => '0900000003');
      gaDangNhapTraVe('PATIENT');

      await repo.signIn(phoneNumber: '0900000003', password: 'Aa123456@');

      verifyNever(() => storage.delete(key: StorageKeys.biometricEnabled));
    });
  });

  group('Dang xuat', () {
    test('Xoa sach ca ba khoa', () async {
      await repo.signOut();

      verify(() => storage.delete(key: StorageKeys.accessToken)).called(1);
      verify(() => storage.delete(key: StorageKeys.pairedPhone)).called(1);
      verify(() => storage.delete(key: StorageKeys.biometricEnabled)).called(1);
    });
  });

  group('Dieu kien hien nut van tay', () {
    test('Khong con token thi KHONG coi la da ghep doi', () async {
      // Đây chính là lỗi cũ: nút vân tay vẫn hiện nhưng bấm vào chỉ báo hết phiên.
      when(() => storage.read(key: StorageKeys.pairedPhone))
          .thenAnswer((_) async => '0900000003');
      when(() => storage.read(key: StorageKeys.biometricEnabled))
          .thenAnswer((_) async => 'true');
      when(() => storage.read(key: StorageKeys.accessToken))
          .thenAnswer((_) async => null);

      expect(await repo.isBiometricPaired(), isFalse);
    });

    test('Da ghep doi, da bat, con token thi moi hien', () async {
      when(() => storage.read(key: StorageKeys.pairedPhone))
          .thenAnswer((_) async => '0900000003');
      when(() => storage.read(key: StorageKeys.biometricEnabled))
          .thenAnswer((_) async => 'true');
      when(() => storage.read(key: StorageKeys.accessToken))
          .thenAnswer((_) async => 'token-gia');

      expect(await repo.isBiometricPaired(), isTrue);
    });

    test('Chua bat tinh nang thi khong hien du con token', () async {
      when(() => storage.read(key: StorageKeys.pairedPhone))
          .thenAnswer((_) async => '0900000003');
      when(() => storage.read(key: StorageKeys.biometricEnabled))
          .thenAnswer((_) async => null);
      when(() => storage.read(key: StorageKeys.accessToken))
          .thenAnswer((_) async => 'token-gia');

      expect(await repo.isBiometricPaired(), isFalse);
    });
  });
}
