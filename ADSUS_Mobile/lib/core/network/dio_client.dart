import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../constants/api_constants.dart';
import '../constants/storage_keys.dart';

/// Tạo Dio đã cấu hình sẵn cho ADSUS.
///
/// Token được gắn tự động vào mọi request nên các tầng trên không phải bận tâm.
Dio createDioClient(FlutterSecureStorage storage) {
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
    ),
  );

  return dio;
}
