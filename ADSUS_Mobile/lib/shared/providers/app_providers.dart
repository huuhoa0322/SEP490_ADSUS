import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../core/network/dio_client.dart';
import '../../features/auth/data/repositories/auth_repository_impl.dart';
import '../../features/auth/data/repositories/biometric_service.dart';
import '../../features/auth/domain/repositories/auth_repository.dart';
import '../../features/auth/presentation/viewmodels/auth_view_model.dart';
import '../../features/appointment_scheduling/data/repositories/appointment_repository_impl.dart';
import '../../features/appointment_scheduling/data/repositories/symptom_repository_impl.dart';
import '../../features/appointment_scheduling/data/services/calendar_sync_service_impl.dart';
import '../../features/appointment_scheduling/domain/repositories/appointment_repository.dart';
import '../../features/appointment_scheduling/domain/repositories/symptom_repository.dart';
import '../../features/appointment_scheduling/domain/services/calendar_sync_service.dart';
import '../../features/medication_reminder/data/repositories/medication_intake_repository_impl.dart';
import '../../features/medication_reminder/data/repositories/reminder_preference_repository_impl.dart';
import '../../features/medication_reminder/data/repositories/widget_data_repository.dart';
export '../../features/medication_reminder/data/services/widget_sync_service.dart';
import '../../features/medication_reminder/domain/repositories/medication_intake_repository.dart';
import '../../features/medication_reminder/domain/repositories/reminder_preference_repository.dart';
import '../../features/health_log/data/repositories/health_log_repository.dart';
import '../../features/ai_chatbot/data/repositories/ai_chat_repository.dart';
import '../../features/medical_record/data/repositories/medical_record_repository_impl.dart';
import '../../features/medical_record/domain/repositories/medical_record_repository.dart';
import '../../features/engagement/data/repositories/blog_repository_impl.dart';
import '../../features/engagement/domain/repositories/blog_repository.dart';
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

/// Module 8 — Đặt lịch khám (UC-13, UC-14).
///
/// Trả về interface [AppointmentRepository] (không phải Impl) để có thể override bằng
/// mock trong test ViewModel, đúng quy ước 03_mobile.md §9.
final appointmentRepositoryProvider = Provider<AppointmentRepository>((ref) {
  return AppointmentRepositoryImpl(ref.watch(dioProvider));
});

/// Module 8 — Repository cho Symptoms (dùng trong booking)
final symptomRepositoryProvider = Provider<SymptomRepository>((ref) {
  return SymptomRepositoryImpl(ref.watch(dioProvider));
});

/// `SharedPreferences` cần await `getInstance()` ở lần đầu — để không phá pattern
/// sync Provider hiện có, ta dùng FutureProvider. UI/ViewModel chỉ `requireValue`
/// khi thực sự cần (ví dụ: trong action bấm nút), không phải lúc build().
final sharedPreferencesProvider = FutureProvider<SharedPreferences>((ref) {
  return SharedPreferences.getInstance();
});

/// UC-16 — đồng bộ lịch hẹn vào Calendar hệ thống (spec #54, client-only).
///
/// Đăng ký dưới dạng FutureProvider vì cần đợi SharedPreferences sẵn sàng; UI phải
/// handle state AsyncValue trước khi gọi các method trên service.
final calendarSyncServiceProvider =
    FutureProvider<CalendarSyncService>((ref) async {
  final prefs = await ref.watch(sharedPreferencesProvider.future);
  return CalendarSyncServiceImpl(prefs: prefs);
});

/// Module 7 — Medication Intake (UC-11, UC-12).
final medicationIntakeRepositoryProvider =
    Provider<MedicationIntakeRepository>((ref) {
  return MedicationIntakeRepositoryImpl(ref.watch(dioProvider));
});

/// Module 7 — Reminder Preference (SCR-19).
final reminderPreferenceRepositoryProvider =
    Provider<ReminderPreferenceRepository>((ref) {
  return ReminderPreferenceRepositoryImpl(ref.watch(dioProvider));
});

/// ADSUS Medication Widget — sync doses to SharedPreferences for Android widget (T-2.2).
final widgetDataRepositoryProvider = Provider<WidgetDataRepository>((ref) {
  return WidgetDataRepository(ref.watch(medicationIntakeRepositoryProvider));
});

/// Module 9 — Health Log (FT-35, FT-40, FT-41).
final healthLogRepositoryProvider = Provider<HealthLogRepository>((ref) {
  return HealthLogRepository(ref.watch(dioProvider));
});

/// Module 10 — AI Chatbot (FT-39).
final aiChatRepositoryProvider = Provider<AiChatRepository>((ref) {
  return AiChatRepository(ref.watch(dioProvider));
});

/// Module 04 — Medical Record (UC-08).
final medicalRecordRepositoryProvider = Provider<MedicalRecordRepository>((ref) {
  return MedicalRecordRepositoryImpl(ref.watch(dioProvider));
});

/// Module 10 — Blog Sức khỏe (UC-23, BR-02: Patient đã đăng nhập).
final blogRepositoryProvider = Provider<BlogRepository>((ref) {
  return BlogRepositoryImpl(ref.watch(dioProvider));
});
