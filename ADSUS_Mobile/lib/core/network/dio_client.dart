import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../constants/api_constants.dart';
import '../constants/storage_keys.dart';

/// Tạo Dio đã cấu hình sẵn cho ADSUS.
///
/// Token được gắn tự động vào mọi request nên các tầng trên không phải bận tâm.
///
/// [onSessionExpired] được gọi khi máy chủ từ chối token đang dùng. Truyền vào từ ngoài
/// thay vì gọi thẳng view model ở đây, để tầng mạng không phải biết gì về Riverpod.
Dio createDioClient(
  FlutterSecureStorage storage, {
  void Function()? onSessionExpired,
}) {
  final dio = Dio(
    BaseOptions(
      baseUrl: ApiConstants.baseUrl,
      connectTimeout: ApiConstants.timeout,
      receiveTimeout: ApiConstants.timeout,
      headers: {'Content-Type': 'application/json'},
      // Để Dio ném DioException cho mọi mã lỗi, tầng trên bắt và dịch một chỗ.
      validateStatus: (status) => status != null && status < 400,
    ),
  );

  dio.interceptors.add(
    InterceptorsWrapper(
      onRequest: (options, handler) async {
        final token = await storage.read(key: StorageKeys.accessToken);
        if (token != null && token.isNotEmpty) {
          // Backend dùng JwtBearer nên header phải đúng dạng "Bearer <token>".
          options.headers['Authorization'] = 'Bearer $token';
        }
        handler.next(options);
      },
      onError: (error, handler) {
        // Máy chủ kiểm trạng thái tài khoản ở MỌI request. Admin khoá tài khoản (UC-04
        // FT-08) là token đang dùng chết ngay. Không xử lý ở đây thì người bị khoá vẫn ngồi
        // nguyên trong ứng dụng, bấm gì cũng lỗi mà không hiểu vì sao.
        //
        // Chỉ tính khi request CÓ GẮN token. Đăng nhập sai mật khẩu cũng trả 401 nhưng
        // request đó không kèm token — không phân biệt thì nhập sai một lần là bị đá ra.
        final coToken = error.requestOptions.headers.containsKey('Authorization');

        if (error.response?.statusCode == 401 && coToken) {
          onSessionExpired?.call();
        }

        handler.next(error);
      },
    ),
  );

  return dio;
}
