import 'package:adsus_mobile/core/network/api_exception.dart';
import 'package:adsus_mobile/features/auth/domain/repositories/auth_repository.dart';
import 'package:adsus_mobile/features/auth/presentation/viewmodels/forgot_password_view_model.dart';
import 'package:adsus_mobile/shared/providers/app_providers.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

class _MockAuthRepository extends Mock implements AuthRepository {}

void main() {
  late _MockAuthRepository authRepo;
  late ProviderContainer container;

  setUp(() {
    authRepo = _MockAuthRepository();
    container = ProviderContainer(
      overrides: [authRepositoryProvider.overrideWithValue(authRepo)],
    );
  });

  tearDown(() => container.dispose());

  test('submit_Success_SetsSentTrue', () async {
    when(() => authRepo.requestPasswordReset(
          phoneNumber: any(named: 'phoneNumber'),
          email: any(named: 'email'),
        )).thenAnswer((_) async {});

    await container
        .read(forgotPasswordViewModelProvider.notifier)
        .submit(phoneNumber: '0900000000', email: 'a@example.com');

    final state = container.read(forgotPasswordViewModelProvider);
    expect(state.sent, isTrue);
    expect(state.isSending, isFalse);
    expect(state.errorMessage, isNull);
  });

  test('submit_NonMatchingAccountInfo_StillSetsSentTrue', () async {
    // AF-01: backend không bao giờ báo lỗi vì "không khớp tài khoản" — Repository không
    // throw trong trường hợp này, nên ViewModel không có gì để phân biệt (đúng chủ đích,
    // chống dò tài khoản qua màn này).
    when(() => authRepo.requestPasswordReset(
          phoneNumber: any(named: 'phoneNumber'),
          email: any(named: 'email'),
        )).thenAnswer((_) async {});

    await container
        .read(forgotPasswordViewModelProvider.notifier)
        .submit(phoneNumber: '0900000000', email: 'khong-ton-tai@example.com');

    expect(container.read(forgotPasswordViewModelProvider).sent, isTrue);
  });

  test('submit_NetworkFailure_SetsErrorMessageAndDoesNotSetSent', () async {
    when(() => authRepo.requestPasswordReset(
          phoneNumber: any(named: 'phoneNumber'),
          email: any(named: 'email'),
        )).thenThrow(const ApiException('Không kết nối được tới máy chủ.'));

    await container
        .read(forgotPasswordViewModelProvider.notifier)
        .submit(phoneNumber: '0900000000', email: 'a@example.com');

    final state = container.read(forgotPasswordViewModelProvider);
    expect(state.sent, isFalse);
    expect(state.errorMessage, 'Không kết nối được tới máy chủ.');
    expect(state.isSending, isFalse);
  });
}
