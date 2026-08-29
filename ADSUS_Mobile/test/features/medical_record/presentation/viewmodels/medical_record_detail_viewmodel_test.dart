import 'dart:async';

import 'package:adsus_mobile/core/network/api_exception.dart';
import 'package:adsus_mobile/features/medical_record/domain/entities/medical_record_case.dart';
import 'package:adsus_mobile/features/medical_record/domain/entities/medical_record_feedback.dart';
import 'package:adsus_mobile/features/medical_record/domain/repositories/medical_record_repository.dart';
import 'package:adsus_mobile/features/medical_record/presentation/viewmodels/medical_record_detail_viewmodel.dart';
import 'package:adsus_mobile/shared/providers/app_providers.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

class _MockRepo extends Mock implements MedicalRecordRepository {}

MedicalRecordCase _makeCase(String caseId, {String doctorConclusion = 'Ket luan'}) =>
    MedicalRecordCase(
      caseId: caseId,
      visitDate: DateTime(2026, 7, 22),
      status: CaseStatus.end,
      doctorId: 'doctor-1',
      doctorName: 'BS. Le Minh Hoang',
      doctorConclusion: doctorConclusion,
    );

void main() {
  late _MockRepo repo;
  late ProviderContainer container;

  setUp(() {
    repo = _MockRepo();
    container = ProviderContainer(
      overrides: [medicalRecordRepositoryProvider.overrideWithValue(repo)],
    );
  });

  tearDown(() => container.dispose());

  test('loadDetail thanh cong thi state co caseDetail dung caseId, khong con loading', () async {
    when(() => repo.getRecordDetail('case-1'))
        .thenAnswer((_) async => _makeCase('case-1', doctorConclusion: 'Nhan xo tu cung'));

    final notifier = container.read(medicalRecordDetailViewModelProvider.notifier);
    await notifier.loadDetail('case-1');

    final state = container.read(medicalRecordDetailViewModelProvider);
    expect(state.caseId, 'case-1');
    expect(state.caseDetail?.doctorConclusion, 'Nhan xo tu cung');
    expect(state.isLoading, isFalse);
  });

  test('repository nem loi thi state co errorMessage, caseId van duoc gan, caseDetail null', () async {
    when(() => repo.getRecordDetail('case-1'))
        .thenThrow(const ApiException('Khong tai duoc chi tiet luot kham.'));

    final notifier = container.read(medicalRecordDetailViewModelProvider.notifier);
    await notifier.loadDetail('case-1');

    final state = container.read(medicalRecordDetailViewModelProvider);
    expect(state.caseId, 'case-1');
    expect(state.errorMessage, 'Khong tai duoc chi tiet luot kham.');
    expect(state.caseDetail, isNull);
  });

  test('goi loadDetail case khac thi state.caseId doi NGAY, truoc khi cho ket qua moi tra ve', () async {
    // Day la co che THAT SU chan bug "thoang hien du lieu case cu" — Screen se so
    // state.caseId voi widget.caseId, khong con phai suy luan tu caseDetail == null.
    when(() => repo.getRecordDetail('case-1'))
        .thenAnswer((_) async => _makeCase('case-1', doctorConclusion: 'Ket luan A'));

    final notifier = container.read(medicalRecordDetailViewModelProvider.notifier);
    await notifier.loadDetail('case-1');
    expect(container.read(medicalRecordDetailViewModelProvider).caseId, 'case-1');
    expect(
      container.read(medicalRecordDetailViewModelProvider).caseDetail?.doctorConclusion,
      'Ket luan A',
    );

    // Case B chua tra loi ngay (Completer chua complete) — kiem tra state NGAY sau khi
    // goi, truoc khi await xong, la caseId da doi sang 'case-2' va caseDetail cu (A) da
    // bi xoa, khong con hien nham.
    final completer = Completer<MedicalRecordCase>();
    when(() => repo.getRecordDetail('case-2')).thenAnswer((_) => completer.future);

    final pending = notifier.loadDetail('case-2');
    final midState = container.read(medicalRecordDetailViewModelProvider);
    expect(midState.caseId, 'case-2');
    expect(midState.caseDetail, isNull);
    expect(midState.isLoading, isTrue);

    completer.complete(_makeCase('case-2', doctorConclusion: 'Ket luan B'));
    await pending;

    final finalState = container.read(medicalRecordDetailViewModelProvider);
    expect(finalState.caseId, 'case-2');
    expect(finalState.caseDetail?.doctorConclusion, 'Ket luan B');
  });

  test(
    'request cua case cu tra loi TRE (out-of-order) thi khong duoc ghi de len case moi hon',
    () async {
      // Khoa lai bug Important tim thay qua whole-branch review 15/08/2026: check
      // state.caseId o Screen chan duoc "thoang hien" khi bam lien tiep, nhung KHONG chan
      // duoc truong hop 2 request chong nhau tra loi SAI THU TU — request cua case A (goi
      // truoc) tra loi SAU request cua case B (goi sau). Neu khong co guard trong
      // loadDetail(), du lieu cua A se ghi de len state dang mang caseId = 'case-b',
      // khien man hinh hien nham chan doan/anh cua benh nhan A duoi case cua benh nhan B.
      final completerA = Completer<MedicalRecordCase>();
      final completerB = Completer<MedicalRecordCase>();
      when(() => repo.getRecordDetail('case-a')).thenAnswer((_) => completerA.future);
      when(() => repo.getRecordDetail('case-b')).thenAnswer((_) => completerB.future);

      final notifier = container.read(medicalRecordDetailViewModelProvider.notifier);
      final pendingA = notifier.loadDetail('case-a');
      final pendingB = notifier.loadDetail('case-b');

      // B tra loi TRUOC (dung thu tu voi luc goi), A tra loi SAU (out-of-order).
      completerB.complete(_makeCase('case-b', doctorConclusion: 'Ket luan B'));
      await pendingB;
      completerA.complete(_makeCase('case-a', doctorConclusion: 'Ket luan A'));
      await pendingA;

      final finalState = container.read(medicalRecordDetailViewModelProvider);
      expect(finalState.caseId, 'case-b');
      expect(
        finalState.caseDetail?.doctorConclusion,
        'Ket luan B',
        reason: 'Du lieu cua case-a (goi truoc, tra loi sau) khong duoc ghi de len case-b',
      );
    },
  );

  group('loadFeedback (FT-37)', () {
    MedicalRecordFeedback makeFeedback() => MedicalRecordFeedback(
          id: 'feedback-1',
          rating: 5,
          content: 'Bac si rat tan tam',
          submittedAt: DateTime(2026, 8, 20),
        );

    test('thanh cong thi state.feedback duoc gan dung', () async {
      when(() => repo.getRecordDetail('case-1'))
          .thenAnswer((_) async => _makeCase('case-1'));
      when(() => repo.getCaseFeedback('case-1')).thenAnswer((_) async => makeFeedback());

      final notifier = container.read(medicalRecordDetailViewModelProvider.notifier);
      await notifier.loadDetail('case-1');
      await notifier.loadFeedback('case-1');

      final state = container.read(medicalRecordDetailViewModelProvider);
      expect(state.feedback?.rating, 5);
    });

    test('chua gui feedback (repo tra null) thi state.feedback la null, khong loi', () async {
      when(() => repo.getRecordDetail('case-1'))
          .thenAnswer((_) async => _makeCase('case-1'));
      when(() => repo.getCaseFeedback('case-1')).thenAnswer((_) async => null);

      final notifier = container.read(medicalRecordDetailViewModelProvider.notifier);
      await notifier.loadDetail('case-1');
      await notifier.loadFeedback('case-1');

      final state = container.read(medicalRecordDetailViewModelProvider);
      expect(state.feedback, isNull);
      expect(state.errorMessage, isNull);
    });

    test('repo nem ApiException thi bi nuot, KHONG hien errorMessage (feedback la optional)',
        () async {
      when(() => repo.getRecordDetail('case-1'))
          .thenAnswer((_) async => _makeCase('case-1'));
      when(() => repo.getCaseFeedback('case-1'))
          .thenThrow(const ApiException('Khong tai duoc phan hoi.'));

      final notifier = container.read(medicalRecordDetailViewModelProvider.notifier);
      await notifier.loadDetail('case-1');
      await notifier.loadFeedback('case-1');

      final state = container.read(medicalRecordDetailViewModelProvider);
      expect(state.errorMessage, isNull);
      expect(state.feedback, isNull);
    });

    test('caseId khac state.caseId hien tai thi khong goi repo (chan case cu)', () async {
      when(() => repo.getRecordDetail('case-1'))
          .thenAnswer((_) async => _makeCase('case-1'));

      final notifier = container.read(medicalRecordDetailViewModelProvider.notifier);
      await notifier.loadDetail('case-1');
      await notifier.loadFeedback('case-2');

      verifyNever(() => repo.getCaseFeedback('case-2'));
    });
  });

  group('submitFeedback (FT-37)', () {
    test('thanh cong thi tai lai feedback, tat loading, khong con error', () async {
      when(() => repo.getRecordDetail('case-1'))
          .thenAnswer((_) async => _makeCase('case-1'));
      when(() => repo.submitCaseFeedback('case-1', 5, 'Rat hai long'))
          .thenAnswer((_) async {});
      when(() => repo.getCaseFeedback('case-1')).thenAnswer(
        (_) async => MedicalRecordFeedback(
          id: 'feedback-1',
          rating: 5,
          content: 'Rat hai long',
          submittedAt: DateTime(2026, 8, 20),
        ),
      );

      final notifier = container.read(medicalRecordDetailViewModelProvider.notifier);
      await notifier.loadDetail('case-1');
      await notifier.submitFeedback('case-1', 5, 'Rat hai long');

      final state = container.read(medicalRecordDetailViewModelProvider);
      expect(state.isLoading, isFalse);
      expect(state.errorMessage, isNull);
      expect(state.feedback?.rating, 5);
      verify(() => repo.submitCaseFeedback('case-1', 5, 'Rat hai long')).called(1);
    });

    test('repo nem ApiException thi state co errorMessage, tat loading', () async {
      when(() => repo.getRecordDetail('case-1'))
          .thenAnswer((_) async => _makeCase('case-1'));
      when(() => repo.submitCaseFeedback('case-1', 1, null))
          .thenThrow(const ApiException('Khong gui duoc phan hoi.'));

      final notifier = container.read(medicalRecordDetailViewModelProvider.notifier);
      await notifier.loadDetail('case-1');
      await notifier.submitFeedback('case-1', 1, null);

      final state = container.read(medicalRecordDetailViewModelProvider);
      expect(state.isLoading, isFalse);
      expect(state.errorMessage, 'Khong gui duoc phan hoi.');
    });

    test('caseId khac state.caseId hien tai thi khong goi repo (chan case cu)', () async {
      when(() => repo.getRecordDetail('case-1'))
          .thenAnswer((_) async => _makeCase('case-1'));

      final notifier = container.read(medicalRecordDetailViewModelProvider.notifier);
      await notifier.loadDetail('case-1');
      await notifier.submitFeedback('case-2', 5, null);

      verifyNever(() => repo.submitCaseFeedback('case-2', any(), any()));
    });
  });
}
