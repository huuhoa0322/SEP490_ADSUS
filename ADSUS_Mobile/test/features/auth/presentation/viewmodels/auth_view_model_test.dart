import 'package:adsus_mobile/features/auth/data/repositories/biometric_service.dart';
import 'package:adsus_mobile/features/auth/domain/entities/auth_session.dart';
import 'package:adsus_mobile/features/auth/domain/repositories/auth_repository.dart';
import 'package:adsus_mobile/features/auth/presentation/viewmodels/auth_view_model.dart';
import 'package:adsus_mobile/features/medical_record/domain/repositories/medical_record_repository.dart';
import 'package:adsus_mobile/features/medical_record/presentation/viewmodels/medical_record_list_viewmodel.dart';
import 'package:adsus_mobile/shared/providers/app_providers.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

class _MockAuthRepository extends Mock implements AuthRepository {}

class _MockBiometricService extends Mock implements BiometricService {}

class _MockMedicalRecordRepository extends Mock implements MedicalRecordRepository {}

void main() {
  late _MockAuthRepository authRepo;
  late _MockBiometricService biometricService;
  late _MockMedicalRecordRepository medicalRepo;
  late ProviderContainer container;

  setUp(() {
    authRepo = _MockAuthRepository();
    biometricService = _MockBiometricService();
    medicalRepo = _MockMedicalRecordRepository();

    // Đủ để constructor AuthViewModel chạy _loadBiometricStatus() không nem loi.
    when(() => biometricService.isAvailable()).thenAnswer((_) async => false);
    when(() => authRepo.isBiometricPaired()).thenAnswer((_) async => false);
    when(() => medicalRepo.getMyRecords()).thenAnswer((_) async => []);

    container = ProviderContainer(
      overrides: [
        authRepositoryProvider.overrideWithValue(authRepo),
        biometricServiceProvider.overrideWithValue(biometricService),
        medicalRecordRepositoryProvider.overrideWithValue(medicalRepo),
      ],
    );
  });

  tearDown(() => container.dispose());

  test(
    'signIn invalidates medicalRecordListViewModelProvider so patient B khong thay du lieu cu cua A',
    () async {
      // Khoa lai bug tim thay qua smoke test 14/08/2026: truoc do chi signOut()/
      // handleSessionExpired() invalidate 2 provider Module 04, con signIn()/
      // signInWithBiometric() thi khong - du da co san pattern nay cho profileViewModelProvider.
      when(() => authRepo.signIn(
            phoneNumber: any(named: 'phoneNumber'),
            password: any(named: 'password'),
          )).thenAnswer((_) async => const AuthSession(
            accessToken: 'token-b',
            fullName: 'Benh nhan B',
            role: UserRole.patient,
            mustChangePassword: false,
          ));

      // Doc lan dau -> Notifier.build() chay -> microtask goi getMyRecords() 1 lan.
      container.read(medicalRecordListViewModelProvider);
      await Future<void>.delayed(Duration.zero);
      verify(() => medicalRepo.getMyRecords()).called(1);

      final result = await container
          .read(authViewModelProvider.notifier)
          .signIn('0900000000', 'matkhau123');
      expect(result, isTrue);

      // signIn() phai invalidate -> doc lai provider se build() lai -> goi them 1 lan nua.
      container.read(medicalRecordListViewModelProvider);
      await Future<void>.delayed(Duration.zero);
      verify(() => medicalRepo.getMyRecords()).called(1);
    },
  );
}
