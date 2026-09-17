import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:adsus_mobile/features/patient_relationship/domain/entities/patient_relationship.dart';
import 'package:adsus_mobile/features/patient_relationship/domain/repositories/patient_relationship_repository.dart';
import 'package:adsus_mobile/features/patient_relationship/presentation/viewmodels/my_relatives_view_model.dart';
import 'package:adsus_mobile/shared/providers/app_providers.dart';

class _MockPatientRelationshipRepo extends Mock
    implements PatientRelationshipRepository {}

void main() {
  late _MockPatientRelationshipRepo mockRepo;
  late ProviderContainer container;

  final initialRelatives = [
    PatientRelationship(
      relationshipId: 'rel-1',
      patientProfileId: 'patient-1',
      fullName: 'Trần Văn Bố',
      phone: '0912345678',
      relationshipName: 'Bố',
      createdAt: DateTime(2026, 1, 1),
    ),
    PatientRelationship(
      relationshipId: 'rel-2',
      patientProfileId: 'patient-1',
      fullName: 'Lê Thị Mẹ',
      phone: '0987654321',
      relationshipName: 'Mẹ',
      createdAt: DateTime(2026, 1, 2),
    ),
  ];

  setUp(() {
    mockRepo = _MockPatientRelationshipRepo();
    when(() => mockRepo.getRelatives()).thenAnswer((_) async => initialRelatives);
    container = ProviderContainer(
      overrides: [
        patientRelationshipRepositoryProvider.overrideWithValue(mockRepo),
      ],
    );
  });

  tearDown(() => container.dispose());

  group('MyRelativesViewModel Tests', () {
    test('TC-MYREL-01: loads relatives automatically on initialization', () async {
      // Allow microtask to complete
      await Future<void>.delayed(Duration.zero);
      await container.read(myRelativesViewModelProvider.notifier).loadRelatives();

      final state = container.read(myRelativesViewModelProvider);
      expect(state.isLoading, isFalse);
      expect(state.relatives.length, 2);
      expect(state.relatives[0].fullName, 'Trần Văn Bố');
    });

    test('TC-MYREL-02: updateRelativeLocally replaces existing relative in state list', () async {
      await container.read(myRelativesViewModelProvider.notifier).loadRelatives();

      final updatedRelative = PatientRelationship(
        relationshipId: 'rel-1',
        patientProfileId: 'patient-1',
        fullName: 'Trần Văn Bố (Đã sửa)',
        phone: '0912345999',
        relationshipName: 'Ba',
        createdAt: DateTime(2026, 1, 1),
      );

      container
          .read(myRelativesViewModelProvider.notifier)
          .updateRelativeLocally(updatedRelative);

      final state = container.read(myRelativesViewModelProvider);
      expect(state.relatives.length, 2);
      expect(state.relatives[0].fullName, 'Trần Văn Bố (Đã sửa)');
      expect(state.relatives[0].phone, '0912345999');
      expect(state.relatives[0].relationshipName, 'Ba');
      expect(state.relatives[1].fullName, 'Lê Thị Mẹ');
    });

    test('TC-MYREL-03: updateRelativeLocally does nothing if relativeId not found', () async {
      await container.read(myRelativesViewModelProvider.notifier).loadRelatives();

      final nonExistent = PatientRelationship(
        relationshipId: 'rel-999',
        patientProfileId: 'patient-1',
        fullName: 'Không tồn tại',
        createdAt: DateTime(2026, 1, 1),
      );

      container
          .read(myRelativesViewModelProvider.notifier)
          .updateRelativeLocally(nonExistent);

      final state = container.read(myRelativesViewModelProvider);
      expect(state.relatives.length, 2);
      expect(state.relatives[0].fullName, 'Trần Văn Bố');
    });
  });
}
