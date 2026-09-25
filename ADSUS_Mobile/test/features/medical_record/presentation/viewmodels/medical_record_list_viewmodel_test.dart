import 'package:adsus_mobile/core/network/api_exception.dart';
import 'package:adsus_mobile/features/medical_record/domain/entities/medical_record_summary.dart';
import 'package:adsus_mobile/features/medical_record/domain/entities/medical_record_case.dart';
import 'package:adsus_mobile/features/medical_record/domain/repositories/medical_record_repository.dart';
import 'package:adsus_mobile/features/medical_record/presentation/viewmodels/medical_record_list_viewmodel.dart';
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

  test('load thanh cong thi state co du records, khong con loading', () async {
    when(() => repo.getMyRecords()).thenAnswer((_) async => [
          MedicalRecordSummary(
            caseId: 'case-1',
            visitDate: DateTime(2026, 7, 22),
            status: CaseStatus.confirmed,
            doctorId: 'doctor-1',
          ),
        ]);
    when(() => repo.getRelativeCases()).thenAnswer((_) async => [
          MedicalRecordSummary(
            caseId: 'case-2',
            visitDate: DateTime(2026, 8, 10),
            status: CaseStatus.end,
            doctorId: 'doctor-1',
            patientName: 'Nguyễn Thị B',
            relationshipName: 'Mẹ',
          ),
        ]);

    final notifier = container.read(medicalRecordListViewModelProvider.notifier);
    await notifier.load();

    final state = container.read(medicalRecordListViewModelProvider);
    // Mặc định tab SELF → records trả về selfRecords
    expect(state.selfRecords, hasLength(1));
    expect(state.relativeRecords, hasLength(1));
    expect(state.records, hasLength(1)); // filteredRecords (SELF tab)
    expect(state.isLoading, isFalse);
    expect(state.errorMessage, isNull);
  });

  test('repository nem loi thi state co errorMessage, records rong', () async {
    when(() => repo.getMyRecords())
        .thenThrow(const ApiException('Khong tai duoc danh sach luot kham.'));
    when(() => repo.getRelativeCases()).thenAnswer((_) async => []);

    final notifier = container.read(medicalRecordListViewModelProvider.notifier);
    await notifier.load();

    final state = container.read(medicalRecordListViewModelProvider);
    expect(state.errorMessage, 'Khong tai duoc danh sach luot kham.');
    expect(state.records, isEmpty);
  });

  test('setFilterScope chuyen tab RELATIVE thi records la relativeRecords', () async {
    when(() => repo.getMyRecords()).thenAnswer((_) async => [
          MedicalRecordSummary(
            caseId: 'case-self',
            visitDate: DateTime(2026, 7, 22),
            status: CaseStatus.end,
            doctorId: 'doctor-1',
          ),
        ]);
    when(() => repo.getRelativeCases()).thenAnswer((_) async => [
          MedicalRecordSummary(
            caseId: 'case-rel-1',
            visitDate: DateTime(2026, 8, 10),
            status: CaseStatus.end,
            doctorId: 'doctor-1',
            patientName: 'Nguyễn Thị B',
            relationshipName: 'Mẹ',
          ),
          MedicalRecordSummary(
            caseId: 'case-rel-2',
            visitDate: DateTime(2026, 9, 5),
            status: CaseStatus.end,
            doctorId: 'doctor-2',
            patientName: 'Nguyễn Văn C',
            relationshipName: 'Con',
          ),
        ]);

    final notifier = container.read(medicalRecordListViewModelProvider.notifier);
    await notifier.load();

    // Chuyển sang tab người thân
    notifier.setFilterScope('RELATIVE');

    final state = container.read(medicalRecordListViewModelProvider);
    expect(state.filterScope, 'RELATIVE');
    expect(state.records, hasLength(2)); // 2 relative records
    expect(state.records.first.patientName, 'Nguyễn Thị B');
    expect(state.selfCount, 1);
    expect(state.relativeCount, 2);
  });
}
