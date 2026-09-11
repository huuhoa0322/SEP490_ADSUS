import 'dart:async';

import 'package:adsus_mobile/core/network/api_exception.dart';
import 'package:adsus_mobile/features/medication_reminder/domain/entities/intake_log.dart';
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
    when(() => repo.getMyIntakeLogs()).thenAnswer((_) async => []);
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

    // Mock getMyIntakeLogs de invalidate() khong throw khi confirmIntake goi _ref.invalidate.
    when(() => repo.getMyIntakeLogs()).thenAnswer((_) async => []);

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

  test('confirmIntake thanh cong patch ngay 1 item vao intakeLogsProvider — khong choi refetch cham', () async {
    final initialLog = IntakeLog(
      intakeId: 'intake-PATCH',
      prescriptionItemId: 'pi-1',
      scheduledTimeUtc: DateTime.utc(2026, 9, 26, 8, 0),
      status: IntakeStatus.pending,
      confirmedAtUtc: null,
      medicineName: 'Amoxicillin',
      dosage: '500mg',
      instructions: null,
    );

    // Khi provider fetch lan dau, tra ve log PENDING.
    when(() => repo.getMyIntakeLogs()).thenAnswer((_) async => [initialLog]);

    // Tao container rieng cho test nay — doc nhanh gia tri dau tien.
    final container2 = ProviderContainer(
      overrides: [medicationIntakeRepositoryProvider.overrideWithValue(repo)],
    );

    // Lay notifier confirm.
    final confirmNotifier = container2.read(intakeListViewModelProvider.notifier);

    // Await future truoc de dam bao provider da resolve data dau tien.
    await container2.read(intakeLogsProvider.future);

    // Xac nhan gia tri ban dau la PENDING, chua co confirmedAt.
    final before = container2.read(intakeLogsProvider);
    expect(before.hasValue, isTrue, reason: 'Provider phai co data ngay sau khi resolve.');
    final logBefore = before.value!.first;
    expect(logBefore.status, IntakeStatus.pending);
    expect(logBefore.confirmedAtUtc, isNull,
        reason: 'Ban dau chua xac nhan — trang thai PENDING.');

    // Mock confirmIntake thanh cong (POST /confirm tra 204).
    when(() => repo.confirmIntake('intake-PATCH')).thenAnswer((_) async {});

    // Patch ngay sau khi confirm thanh cong.
    // Muc tieu: sau confirmIntake, intakeLogsProvider da co item da patch
    // ma KHONG phu thuoc vao invalidate() (neu co thi no chay nen).
    await confirmNotifier.confirmIntake('intake-PATCH');

    // Doc state sau khi patch (invalidate() chay nen co the la AsyncLoading,
    // nhung neu patch duoc goi truoc thi data da duoc cap nhat).
    final after = container2.read(intakeLogsProvider);

    // Case 1: invalidate() chay nhanh, state = loading → skip kiem tra value.
    // Case 2: patchConfirmed duoc goi, state = data voi item da patch.
    if (after.hasValue) {
      final patched = (after.value as List<dynamic>).cast<IntakeLog>().first;
      expect(patched.intakeId, 'intake-PATCH');
      expect(patched.confirmedAtUtc, isNotNull,
          reason: 'Sau confirm, confirmedAtUtc phai duoc set ngay — khong choi refetch.');
      expect(patched.status, IntakeStatus.taken,
          reason: 'Status phai chuyen thanh TAKEN ngay luc patch.');
    } else {
      // Neu state dang loading (invalidate cham hon patch), cho no resolve roi kiem tra.
      await container2.read(intakeLogsProvider.future);
      final resolved = container2.read(intakeLogsProvider);
      expect(resolved.hasValue, isTrue);
      final patched = resolved.value!.first;
      expect(patched.confirmedAtUtc, isNotNull,
          reason: 'Sau khi resolve, confirmedAtUtc phai co gia tri.');
      expect(patched.status, IntakeStatus.taken);
    }

    container2.dispose();
  });
}
