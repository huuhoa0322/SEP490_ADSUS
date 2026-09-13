import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:adsus_mobile/features/patient_relationship/domain/entities/patient_relationship.dart';
import 'package:adsus_mobile/features/patient_relationship/domain/repositories/patient_relationship_repository.dart';
import 'package:adsus_mobile/features/patient_relationship/presentation/viewmodels/add_relative_view_model.dart';
import 'package:adsus_mobile/shared/providers/app_providers.dart';

class _MockPatientRelationshipRepo extends Mock implements PatientRelationshipRepository {}

void main() {
  late _MockPatientRelationshipRepo mockRepo;
  late ProviderContainer container;

  setUp(() {
    mockRepo = _MockPatientRelationshipRepo();
    container = ProviderContainer(
      overrides: [
        patientRelationshipRepositoryProvider.overrideWithValue(mockRepo),
      ],
    );
  });

  tearDown(() => container.dispose());

  group('AddRelativeViewModel Tests', () {
    test('TC-FE-07: AddRelativeVM_UpdateFields updates state and clears error', () {
      final notifier = container.read(addRelativeViewModelProvider.notifier);

      notifier.updateFullName('Nguyễn Văn A');
      notifier.updatePhone('0912345678');
      notifier.updateRelationshipName('Anh trai');

      final state = container.read(addRelativeViewModelProvider);
      expect(state.fullName, 'Nguyễn Văn A');
      expect(state.phone, '0912345678');
      expect(state.relationshipName, 'Anh trai');
      expect(state.errorMessage, isNull);
    });

    test('TC-FE-08: AddRelativeVM_ElderlyNullPhone_CanSave allows saving elderly without phone', () {
      final notifier = container.read(addRelativeViewModelProvider.notifier);

      notifier.updateFullName('Bà Ngoại');
      notifier.updatePhone(''); // Phone empty for elderly
      notifier.updateRelationshipName('Bà Ngoại');

      final state = container.read(addRelativeViewModelProvider);
      expect(state.fullName, 'Bà Ngoại');
      expect(state.phone, isEmpty);
      expect(state.isValid, isTrue);
      expect(state.canSave, isTrue);
    });

    test('TC-FE-09: AddRelativeVM_PhoneRegistered_CanSaveFalse blocks saving registered phone', () async {
      when(() => mockRepo.checkPhoneRegistered('0900111222'))
          .thenAnswer((_) async => true);

      final notifier = container.read(addRelativeViewModelProvider.notifier);
      notifier.updateFullName('Người Thân');
      notifier.updatePhone('0900111222');

      await notifier.checkPhone();

      final state = container.read(addRelativeViewModelProvider);
      expect(state.isPhoneChecked, isTrue);
      expect(state.isPhoneRegistered, isTrue);
      expect(state.canSave, isFalse);
    });

    test('TC-FE-10: AddRelativeVM_SaveRelative_Success calls repository and saves relative', () async {
      final expectedRelative = PatientRelationship(
        relationshipId: 'rel-saved-1',
        patientProfileId: 'prof-saved-1',
        fullName: 'Trần Thị Vợ',
        phone: '0912345678',
        relationshipName: 'Vợ',
        createdAt: DateTime.now(),
      );

      when(() => mockRepo.checkPhoneRegistered('0912345678'))
          .thenAnswer((_) async => false);

      when(() => mockRepo.addRelative(
            fullName: 'Trần Thị Vợ',
            phone: '0912345678',
            dateOfBirth: any(named: 'dateOfBirth'),
            relationshipName: 'Vợ',
          )).thenAnswer((_) async => expectedRelative);

      final notifier = container.read(addRelativeViewModelProvider.notifier);
      notifier.updateFullName('Trần Thị Vợ');
      notifier.updatePhone('0912345678');
      notifier.updateRelationshipName('Vợ');

      final success = await notifier.saveRelative();

      expect(success, isTrue);
      final state = container.read(addRelativeViewModelProvider);
      expect(state.savedRelative, isNotNull);
      expect(state.savedRelative?.relationshipId, 'rel-saved-1');
      expect(state.savedRelative?.fullName, 'Trần Thị Vợ');
      expect(state.isSaving, isFalse);
    });
  });
}
