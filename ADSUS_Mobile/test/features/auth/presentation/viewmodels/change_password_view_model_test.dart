import 'package:adsus_mobile/core/network/api_exception.dart';
import 'package:adsus_mobile/features/auth/data/repositories/biometric_service.dart';
import 'package:adsus_mobile/features/auth/domain/entities/auth_session.dart';
import 'package:adsus_mobile/features/auth/domain/repositories/auth_repository.dart';
import 'package:adsus_mobile/features/auth/presentation/viewmodels/auth_view_model.dart';
import 'package:adsus_mobile/features/auth/presentation/viewmodels/change_password_view_model.dart';
import 'package:adsus_mobile/features/medical_record/domain/repositories/medical_record_repository.dart';
import 'package:adsus_mobile/shared/providers/app_providers.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

class _MockAuthRepository extends Mock implements AuthRepository {}

class _MockBiometricService extends Mock implements BiometricService {}

class _MockMedicalRecordRepository extends Mock implements MedicalRecordRepository {}

void main() {
  late _MockAuthRepository authRepo;
  late ProviderContainer container;

  setUp(() {
    authRepo = _MockAuthRepository();
    final biometricService = _MockBiometricService();
    final medicalRepo = _MockMedicalRecordRepository();

    // AuthViewModel's constructor calls _loadBiometricStatus() — needed because
    // clearMustChangePassword() (called on submit success) reads authViewModelProvider.
    when(() => biometricService.isAvailable()).thenAnswer((_) async => false);
    when(() => authRepo.isBiometricPaired()).thenAnswer((_) async => false);
    // AuthViewModel.signIn() invalidates medicalRecordListViewModelProvider, which rebuilds
    // and calls getMyRecords() — must be stubbed or mocktail returns null for an unstubbed
    // Future<List<...>>, crashing the rebuild (same stub as auth_view_model_test.dart).
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

  test('submit_Success_ReturnsTrueAndSetsSucceeded', () async {
    when(() => authRepo.changePassword(
          currentPassword: any(named: 'currentPassword'),
          newPassword: any(named: 'newPassword'),
          confirmNewPassword: any(named: 'confirmNewPassword'),
        )).thenAnswer((_) async {});

    final result = await container.read(changePasswordViewModelProvider.notifier).submit(
          currentPassword: 'Old@123',
          newPassword: 'New@456',
          confirmNewPassword: 'New@456',
        );

    expect(result, isTrue);
    final state = container.read(changePasswordViewModelProvider);
    expect(state.succeeded, isTrue);
    expect(state.isSaving, isFalse);
    expect(state.errorMessage, isNull);
  });

  test('submit_Success_ClearsMustChangePasswordOnAuthViewModel', () async {
    when(() => authRepo.signIn(
          phoneNumber: any(named: 'phoneNumber'),
          password: any(named: 'password'),
        )).thenAnswer((_) async => const AuthSession(
          accessToken: 'tok',
          fullName: 'Nguyen Van A',
          role: UserRole.patient,
          mustChangePassword: true,
        ));
    await container.read(authViewModelProvider.notifier).signIn('0900000000', 'temp');
    expect(container.read(authViewModelProvider).session?.mustChangePassword, isTrue);

    when(() => authRepo.changePassword(
          currentPassword: any(named: 'currentPassword'),
          newPassword: any(named: 'newPassword'),
          confirmNewPassword: any(named: 'confirmNewPassword'),
        )).thenAnswer((_) async {});

    await container.read(changePasswordViewModelProvider.notifier).submit(
          currentPassword: null,
          newPassword: 'New@456',
          confirmNewPassword: 'New@456',
        );

    // Backend đã gỡ cờ trong DB, ViewModel phải gỡ theo ở AuthViewModel để AuthGuard/router
    // thôi chặn màn khác — đây là hành vi thật ChangePasswordViewModel.submit() thực hiện.
    expect(container.read(authViewModelProvider).session?.mustChangePassword, isFalse);
  });

  test('submit_RepositoryThrows_SetsErrorMessageAndReturnsFalse', () async {
    when(() => authRepo.changePassword(
          currentPassword: any(named: 'currentPassword'),
          newPassword: any(named: 'newPassword'),
          confirmNewPassword: any(named: 'confirmNewPassword'),
        )).thenThrow(const ApiException('Mật khẩu hiện tại không đúng.'));

    final result = await container.read(changePasswordViewModelProvider.notifier).submit(
          currentPassword: 'Wrong',
          newPassword: 'New@456',
          confirmNewPassword: 'New@456',
        );

    expect(result, isFalse);
    final state = container.read(changePasswordViewModelProvider);
    expect(state.succeeded, isFalse);
    expect(state.errorMessage, 'Mật khẩu hiện tại không đúng.');
    expect(state.isSaving, isFalse);
  });
}
