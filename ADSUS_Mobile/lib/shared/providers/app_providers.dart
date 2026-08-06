import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../../core/network/dio_client.dart';
import '../../features/auth/data/repositories/auth_repository_impl.dart';
import '../../features/auth/data/repositories/biometric_service.dart';
import '../../features/auth/domain/repositories/auth_repository.dart';
import '../../features/auth/presentation/viewmodels/auth_view_model.dart';
import '../../features/medication_reminder/data/repositories/medication_intake_repository_impl.dart';
import '../../features/medication_reminder/domain/repositories/medication_intake_repository.dart';

/// Kho lưu trữ được hệ điều hành mã hoá (Keystore/Keychain).
final secureStorageProvider = Provider<FlutterSecureStorage>((ref) {
  // Mặc định thư viện đã dùng Keystore của Android và Keychain của iOS.
  // Không đặt encryptedSharedPreferences vì tuỳ chọn đó đã bị khai tử.
  return const FlutterSecureStorage();
});

final dioProvider = Provider<Dio>((ref) {
  return createDioClient(
    ref.watch(secureStorageProvider),
    // ref.read chỉ chạy KHI thật sự nhận 401, không chạy lúc dựng provider — nên không tạo
    // vòng phụ thuộc dù authViewModel lại phụ thuộc ngược vào dio qua repository.
    onSessionExpired: () =>
        ref.read(authViewModelProvider.notifier).handleSessionExpired(),
  );
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

/// Module 7 — Patient xem lịch uống thuốc + xác nhận đã uống (SCR-19, UC-19/20).
/// Stub cho đến khi team bổ sung giao diện Doctor xem trên Mobile (sprint sau).
final medicationIntakeRepositoryProvider = Provider<MedicationIntakeRepository>((ref) {
  return MedicationIntakeRepositoryImpl(ref.watch(dioProvider));
});
