import 'dart:async';

import 'package:adsus_mobile/core/network/api_exception.dart';
import 'package:adsus_mobile/features/medical_record/domain/entities/medical_record_case.dart';
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
}
