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

  test('loadDetail thanh cong thi state co caseDetail, khong con loading', () async {
    when(() => repo.getRecordDetail('case-1')).thenAnswer((_) async => MedicalRecordCase(
          caseId: 'case-1',
          visitDate: DateTime(2026, 7, 22),
          status: CaseStatus.confirmed,
          doctorId: 'doctor-1',
          conclusion: 'Nhan xo tu cung',
        ));

    final notifier = container.read(medicalRecordDetailViewModelProvider.notifier);
    await notifier.loadDetail('case-1');

    final state = container.read(medicalRecordDetailViewModelProvider);
    expect(state.caseDetail?.conclusion, 'Nhan xo tu cung');
    expect(state.isLoading, isFalse);
  });

  test('repository nem loi thi state co errorMessage, caseDetail null', () async {
    when(() => repo.getRecordDetail('case-1'))
        .thenThrow(const ApiException('Khong tai duoc chi tiet luot kham.'));

    final notifier = container.read(medicalRecordDetailViewModelProvider.notifier);
    await notifier.loadDetail('case-1');

    final state = container.read(medicalRecordDetailViewModelProvider);
    expect(state.errorMessage, 'Khong tai duoc chi tiet luot kham.');
    expect(state.caseDetail, isNull);
  });

  test('goi loadDetail case khac thi xoa caseDetail cu TRUOC KHI cho ket qua moi tra ve', () async {
    when(() => repo.getRecordDetail('case-1')).thenAnswer((_) async => MedicalRecordCase(
          caseId: 'case-1',
          visitDate: DateTime(2026, 7, 22),
          status: CaseStatus.confirmed,
          doctorId: 'doctor-1',
          conclusion: 'Ket luan A',
        ));

    final notifier = container.read(medicalRecordDetailViewModelProvider.notifier);
    await notifier.loadDetail('case-1');
    expect(
      container.read(medicalRecordDetailViewModelProvider).caseDetail?.conclusion,
      'Ket luan A',
    );

    // Case B chua tra loi ngay (Completer chua complete) — kiem tra state NGAY sau khi
    // goi, truoc khi await xong, la caseDetail cu (A) da bi xoa, khong con hien nham.
    final completer = Completer<MedicalRecordCase>();
    when(() => repo.getRecordDetail('case-2')).thenAnswer((_) => completer.future);

    final pending = notifier.loadDetail('case-2');
    expect(container.read(medicalRecordDetailViewModelProvider).caseDetail, isNull);
    expect(container.read(medicalRecordDetailViewModelProvider).isLoading, isTrue);

    completer.complete(MedicalRecordCase(
      caseId: 'case-2',
      visitDate: DateTime(2026, 7, 23),
      status: CaseStatus.confirmed,
      doctorId: 'doctor-2',
      conclusion: 'Ket luan B',
    ));
    await pending;

    expect(
      container.read(medicalRecordDetailViewModelProvider).caseDetail?.conclusion,
      'Ket luan B',
    );
  });
}
