import 'package:adsus_mobile/core/network/api_exception.dart';
import 'package:adsus_mobile/features/auth/domain/entities/auth_session.dart';
import 'package:adsus_mobile/features/auth/domain/entities/user_profile.dart';
import 'package:adsus_mobile/features/auth/domain/repositories/auth_repository.dart';
import 'package:adsus_mobile/features/auth/presentation/viewmodels/profile_view_model.dart';
import 'package:adsus_mobile/shared/providers/app_providers.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

class _MockAuthRepository extends Mock implements AuthRepository {}

void main() {
  late _MockAuthRepository authRepo;
  late ProviderContainer container;

  const profile = UserProfile(
    fullName: 'Nguyen Van A',
    phoneNumber: '0900000000',
    role: UserRole.patient,
    biometricEnabled: false,
  );

  setUp(() {
    authRepo = _MockAuthRepository();
    container = ProviderContainer(
      overrides: [authRepositoryProvider.overrideWithValue(authRepo)],
    );
  });

  tearDown(() => container.dispose());

  test('load_Success_SetsProfile', () async {
    when(() => authRepo.getMyProfile()).thenAnswer((_) async => profile);

    await container.read(profileViewModelProvider.notifier).load();

    final state = container.read(profileViewModelProvider);
    expect(state.profile, profile);
    expect(state.isLoading, isFalse);
    expect(state.errorMessage, isNull);
  });

  test('load_RepositoryThrows_SetsErrorMessage', () async {
    when(() => authRepo.getMyProfile())
        .thenThrow(const ApiException('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.'));

    await container.read(profileViewModelProvider.notifier).load();

    final state = container.read(profileViewModelProvider);
    expect(state.profile, isNull);
    expect(state.errorMessage, 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
  });

  test('save_Success_ReturnsTrueAndReloadsProfileFromServer', () async {
    // save() đọc lại getMyProfile() sau khi update, thay vì tự ghép state — xác nhận đúng
    // hành vi đó bằng cách trả về 1 profile KHÁC với những gì được gửi lên.
    final saved = profile.copyWith(fullName: 'Ten Da Luu Tren Server');
    when(() => authRepo.updateMyProfile(
          fullName: any(named: 'fullName'),
          email: any(named: 'email'),
          dateOfBirth: any(named: 'dateOfBirth'),
        )).thenAnswer((_) async {});
    when(() => authRepo.getMyProfile()).thenAnswer((_) async => saved);

    final result = await container
        .read(profileViewModelProvider.notifier)
        .save(fullName: 'Ten Moi', email: null, dateOfBirth: null);

    expect(result, isTrue);
    final state = container.read(profileViewModelProvider);
    expect(state.profile?.fullName, 'Ten Da Luu Tren Server');
    expect(state.successMessage, isNotNull);
    expect(state.isSaving, isFalse);
  });

  test('save_RepositoryThrows_ReturnsFalseAndSetsErrorMessage', () async {
    when(() => authRepo.updateMyProfile(
          fullName: any(named: 'fullName'),
          email: any(named: 'email'),
          dateOfBirth: any(named: 'dateOfBirth'),
        )).thenThrow(const ApiException('Email này đã có tài khoản khác dùng.'));

    final result = await container
        .read(profileViewModelProvider.notifier)
        .save(fullName: 'Ten Moi', email: 'trung@example.com', dateOfBirth: null);

    expect(result, isFalse);
    expect(container.read(profileViewModelProvider).errorMessage,
        'Email này đã có tài khoản khác dùng.');
  });

  test('setBiometric_Success_UpdatesProfileBiometricFlagLocally', () async {
    // setBiometric() sửa current?.copyWith(...) chứ không load lại — seed state.profile
    // trước để có "current" mà sửa.
    when(() => authRepo.getMyProfile()).thenAnswer((_) async => profile);
    await container.read(profileViewModelProvider.notifier).load();

    when(() => authRepo.setBiometricEnabled(any())).thenAnswer((_) async {});

    final result = await container.read(profileViewModelProvider.notifier).setBiometric(true);

    expect(result, isTrue);
    expect(container.read(profileViewModelProvider).profile?.biometricEnabled, isTrue);
  });

  test('setBiometric_RepositoryThrows_ReturnsFalseAndSetsErrorMessage', () async {
    when(() => authRepo.setBiometricEnabled(any()))
        .thenThrow(const ApiException('Không đổi được cài đặt sinh trắc học.'));

    final result = await container.read(profileViewModelProvider.notifier).setBiometric(true);

    expect(result, isFalse);
    expect(container.read(profileViewModelProvider).errorMessage,
        'Không đổi được cài đặt sinh trắc học.');
  });
}
