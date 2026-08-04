import 'dart:convert';
import 'dart:typed_data';

import 'package:adsus_mobile/core/constants/storage_keys.dart';
import 'package:adsus_mobile/core/network/dio_client.dart';
import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

class _MockStorage extends Mock implements FlutterSecureStorage {}

/// Bộ chuyển đổi giả: luôn trả về mã HTTP đã định sẵn, không đi ra mạng.
class _FakeAdapter implements HttpClientAdapter {
  _FakeAdapter(this.statusCode);

  final int statusCode;

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    return ResponseBody.fromString(
      jsonEncode({'code': statusCode, 'message': 'gia lap', 'data': null}),
      statusCode,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

/// Kiểm thử chốt chặn phiên hết hiệu lực ở tầng mạng.
///
/// Máy chủ kiểm trạng thái tài khoản ở MỌI request, nên Admin khoá một tài khoản (UC-04
/// FT-08) là token đang dùng chết ngay. Không xử lý thì người bị khoá vẫn ngồi nguyên trong
/// ứng dụng, bấm gì cũng lỗi mà không hiểu vì sao.
void main() {
  late _MockStorage storage;
  var soLanGoi = 0;

  setUp(() {
    storage = _MockStorage();
    soLanGoi = 0;
    when(() => storage.read(key: any(named: 'key'))).thenAnswer((_) async => null);
  });

  Dio taoDio({required int ma, String? token}) {
    when(() => storage.read(key: StorageKeys.accessToken))
        .thenAnswer((_) async => token);

    final dio = createDioClient(storage, onSessionExpired: () => soLanGoi++);
    dio.httpClientAdapter = _FakeAdapter(ma);
    return dio;
  }

  test('401 khi request CO gan token thi ket thuc phien', () async {
    final dio = taoDio(ma: 401, token: 'token-con-hieu-luc');

    await expectLater(dio.get<void>('/api/v1/users/me'), throwsA(isA<DioException>()));

    expect(soLanGoi, 1);
  });

  test('401 luc dang nhap sai mat khau thi KHONG ket thuc phien', () async {
    // Đăng nhập sai mật khẩu cũng trả 401, nhưng request đó không kèm token. Không phân biệt
    // hai trường hợp thì nhập sai một lần là bị đá ra khỏi màn đăng nhập.
    final dio = taoDio(ma: 401, token: null);

    await expectLater(dio.post<void>('/api/v1/auth/login'), throwsA(isA<DioException>()));

    expect(soLanGoi, 0);
  });

  test('Loi khac 401 thi khong dung toi phien dang nhap', () async {
    // 500 là máy chủ trục trặc, không có nghĩa là token chết.
    final dio = taoDio(ma: 500, token: 'token-con-hieu-luc');

    await expectLater(dio.get<void>('/api/v1/users/me'), throwsA(isA<DioException>()));

    expect(soLanGoi, 0);
  });

  test('Request thanh cong thi khong dung toi phien dang nhap', () async {
    final dio = taoDio(ma: 200, token: 'token-con-hieu-luc');

    await dio.get<void>('/api/v1/users/me');

    expect(soLanGoi, 0);
  });
}
