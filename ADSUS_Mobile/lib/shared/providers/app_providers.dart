import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../../core/network/dio_client.dart';
import '../../features/auth/data/repositories/auth_repository_impl.dart';
import '../../features/auth/data/repositories/biometric_service.dart';
import '../../features/auth/domain/repositories/auth_repository.dart';

/// Kho lưu trữ được hệ điều hành mã hoá (Keystore/Keychain).
final secureStorageProvider = Provider<FlutterSecureStorage>((ref) {
  // Mặc định thư viện đã dùng Keystore của Android và Keychain của iOS.
  // Không đặt encryptedSharedPreferences vì tuỳ chọn đó đã bị khai tử.
  return const FlutterSecureStorage();
});

final dioProvider = Provider<Dio>((ref) {
  return createDioClient(ref.watch(secureStorageProvider));
});

final authRepositoryProvider = Provider<AuthRepository>((ref) {
  return AuthRepositoryImpl(
    ref.watch(dioProvider),
    ref.watch(secureStorageProvider),
  );
});

final biometricServiceProvider = Provider<BiometricService>((ref) {
  return BiometricService();
});
