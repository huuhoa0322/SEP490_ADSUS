import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:adsus_mobile/features/appointment_scheduling/domain/repositories/appointment_repository.dart';
import 'package:adsus_mobile/features/appointment_scheduling/domain/repositories/symptom_repository.dart';
import 'package:adsus_mobile/features/appointment_scheduling/presentation/viewmodels/book_appointment_view_model.dart';
import 'package:adsus_mobile/features/patient_relationship/domain/entities/patient_relationship.dart';
import 'package:adsus_mobile/features/patient_relationship/domain/repositories/patient_relationship_repository.dart';
import 'package:adsus_mobile/shared/providers/app_providers.dart';

class _MockAppointmentRepo extends Mock implements AppointmentRepository {}

class _MockSymptomRepo extends Mock implements SymptomRepository {}

class _MockPatientRelationshipRepo extends Mock implements PatientRelationshipRepository {}

void main() {
  late _MockAppointmentRepo mockAppointmentRepo;
  late _MockSymptomRepo mockSymptomRepo;
  late _MockPatientRelationshipRepo mockRelationshipRepo;
  late ProviderContainer container;

  final sampleRelative1 = PatientRelationship(
    relationshipId: 'rel-001',
    patientProfileId: 'prof-001',
    fullName: 'Nguyễn Thị Vợ',
    phone: '0901112222',
    relationshipName: 'Vợ',
    createdAt: DateTime.now(),
  );

  final sampleRelative2 = PatientRelationship(
    relationshipId: 'rel-002',
    patientProfileId: 'prof-002',
    fullName: 'Bà Ngoại',
    phone: null,
    relationshipName: 'Bà Ngoại',
    createdAt: DateTime.now(),
  );

  setUp(() {
    mockAppointmentRepo = _MockAppointmentRepo();
    mockSymptomRepo = _MockSymptomRepo();
    mockRelationshipRepo = _MockPatientRelationshipRepo();

    when(() => mockAppointmentRepo.searchOpenSlots()).thenAnswer((_) async => []);
    when(() => mockRelationshipRepo.getRelatives())
        .thenAnswer((_) async => [sampleRelative1, sampleRelative2]);

    container = ProviderContainer(
      overrides: [
        appointmentRepositoryProvider.overrideWithValue(mockAppointmentRepo),
        symptomRepositoryProvider.overrideWithValue(mockSymptomRepo),
        patientRelationshipRepositoryProvider.overrideWithValue(mockRelationshipRepo),
      ],
    );
  });

  tearDown(() => container.dispose());

  group('BookAppointmentViewModel - Relative Booking Flow', () {
    test('TC-FE-11: BookAppointmentVM_ToggleSelfBooking toggles mode and clears relative', () {
      final notifier = container.read(bookAppointmentViewModelProvider.notifier);

      expect(container.read(bookAppointmentViewModelProvider).isBookingForSelf, isTrue);

      // Switch to booking for others
      notifier.setIsBookingForSelf(false);
      expect(container.read(bookAppointmentViewModelProvider).isBookingForSelf, isFalse);

      // Switch back to self-booking
      notifier.setIsBookingForSelf(true);
      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.isBookingForSelf, isTrue);
      expect(state.selectedRelative, isNull);
    });

    test('TC-FE-12: BookAppointmentVM_LoadSavedRelatives loads list into state', () async {
      final notifier = container.read(bookAppointmentViewModelProvider.notifier);

      await notifier.loadSavedRelatives();

      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.isLoadingRelatives, isFalse);
      expect(state.savedRelatives, hasLength(2));
      expect(state.savedRelatives[0].relationshipId, 'rel-001');
      expect(state.savedRelatives[1].relationshipId, 'rel-002');
    });

    test('TC-FE-13: BookAppointmentVM_SelectRelative selects target relative', () async {
      final notifier = container.read(bookAppointmentViewModelProvider.notifier);
      await notifier.loadSavedRelatives();

      notifier.selectRelative('rel-001');

      var state = container.read(bookAppointmentViewModelProvider);
      expect(state.selectedRelative, isNotNull);
      expect(state.selectedRelative?.relationshipId, 'rel-001');
      expect(state.selectedRelative?.fullName, 'Nguyễn Thị Vợ');

      // Deselect relative
      notifier.selectRelative(null);
      state = container.read(bookAppointmentViewModelProvider);
      expect(state.selectedRelative, isNull);
    });

    test('TC-FE-14: BookAppointmentVM_SilentSelfBooking_Blocked blocks booking without relative', () async {
      final notifier = container.read(bookAppointmentViewModelProvider.notifier);

      // Arrange: select a slot, switch to booking for relative, but DO NOT select relative
      notifier.selectSlot('slot-123');
      notifier.setIsBookingForSelf(false);

      // Verify preconditions
      var state = container.read(bookAppointmentViewModelProvider);
      expect(state.selectedSlotId, 'slot-123');
      expect(state.isBookingForSelf, isFalse);
      expect(state.selectedRelative, isNull);

      // Act: Try to book
      await notifier.book();

      // Assert: Booking blocked, error message set, appointment repo never called
      state = container.read(bookAppointmentViewModelProvider);
      expect(state.errorMessage, 'Vui lòng chọn người thân trước khi xác nhận đặt lịch.');
      expect(state.isBooking, isFalse);

      verifyNever(() => mockAppointmentRepo.bookAppointment(
            scheduleSlotId: any(named: 'scheduleSlotId'),
            relationshipId: any(named: 'relationshipId'),
            reason: any(named: 'reason'),
            symptoms: any(named: 'symptoms'),
          ));
    });
  });
}
