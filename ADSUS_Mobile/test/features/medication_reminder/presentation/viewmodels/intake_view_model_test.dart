import 'dart:async';

import 'package:adsus_mobile/core/network/api_exception.dart';
import 'package:adsus_mobile/features/medication_reminder/domain/repositories/medication_intake_repository.dart';
import 'package:adsus_mobile/features/medication_reminder/presentation/viewmodels/intake_view_model.dart';
import 'package:adsus_mobile/shared/providers/app_providers.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

class _MockRepo extends Mock implements MedicationIntakeRepository {}

void main() {
  late _MockRepo repo;
  late ProviderContainer container;

  setUp(() {
    repo = _MockRepo();
    container = ProviderContainer(
      overrides: [medicationIntakeRepositoryProvider.overrideWithValue(repo)],
    );
  });

  tearDown(() => container.dispose());

  test('confirmIntake chi danh dau isSubmitting cho dung intakeId, khong anh huong card khac', () async {
    // Repo confirm se block cho den khi completer xong.
    final completerA = Completer<void>();
    final completerB = Completer<void>();
    when(() => repo.confirmIntake('intake-A')).thenAnswer((_) => completerA.future);
    when(() => repo.confirmIntake('intake-B')).thenAnswer((_) => completerB.future);

    final notifier = container.read(intakeListViewModelProvider.notifier);

    final pendingA = notifier.confirmIntake('intake-A');
    final pendingB = notifier.confirmIntake('intake-B');

    // Ca 2 dang song song confirm — IS BUG TOI: state global isSubmitting
    // cung se la true cho ca 2 (card khac ngoai 2 cai nay cung se spinner).
    // FIX: chuyen sang Set<String> isSubmittingIds.
    final midState = container.read(intakeListViewModelProvider);
    expect(midState.isSubmittingIds, isA<Set<String>>(),
        reason: 'State phai dung Set<String> de track tung intakeId dang submit.');
    expect(midState.isSubmittingIds, contains('intake-A'));
    expect(midState.isSubmittingIds, contains('intake-B'));

    // Hoan tat A truoc B.
    completerA.complete();
    await pendingA;
    final stateAfterA = container.read(intakeListViewModelProvider);
    expect(stateAfterA.isSubmittingIds, isNot(contains('intake-A')),
        reason: 'Sau khi A xong, A phai duoc go khoi set.');
    expect(stateAfterA.isSubmittingIds, contains('intake-B'),
        reason: 'B van dang submit, phai con trong set.');

    completerB.complete();
    await pendingB;
    final finalState = container.read(intakeListViewModelProvider);
    expect(finalState.isSubmittingIds, isEmpty,
        reason: 'Sau khi ca A va B xong, set phai rong.');
  });

  test('confirmIntake that bai van remove intakeId khoi isSubmittingIds va set errorMessage', () async {
    when(() => repo.confirmIntake('intake-FAIL'))
        .thenThrow(const ApiException('Khong ghi nhan duoc'));

    final notifier = container.read(intakeListViewModelProvider.notifier);
    final ok = await notifier.confirmIntake('intake-FAIL');

    expect(ok, isFalse);
    final state = container.read(intakeListViewModelProvider);
    expect(state.isSubmittingIds, isEmpty,
        reason: 'That bai phai clear id khoi set, tranh card bi spinner vinh vien.');
    expect(state.errorMessage, 'Khong ghi nhan duoc');
  });

  test('isSubmittingFor(id) tra ve true/false theo id, khong phai state global', () async {
    final completer = Completer<void>();
    when(() => repo.confirmIntake('intake-X')).thenAnswer((_) => completer.future);

    final notifier = container.read(intakeListViewModelProvider.notifier);
    final pending = notifier.confirmIntake('intake-X');

    expect(container.read(intakeListViewModelProvider).isSubmittingFor('intake-X'), isTrue);
    expect(container.read(intakeListViewModelProvider).isSubmittingFor('intake-KHAC'), isFalse,
        reason: 'Card khac phai Biet duoc no khong dang submit.');
    expect(container.read(intakeListViewModelProvider).isSubmittingFor('intake-KHAC'), isFalse);

    completer.complete();
    await pending;
    expect(container.read(intakeListViewModelProvider).isSubmittingFor('intake-X'), isFalse);
  });
}
