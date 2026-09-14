import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

import 'package:adsus_mobile/core/network/api_exception.dart';
import 'package:adsus_mobile/features/auth/presentation/viewmodels/auth_view_model.dart';
import 'package:adsus_mobile/features/appointment_scheduling/data/dtos/appointment_dtos.dart';
import 'package:adsus_mobile/features/appointment_scheduling/domain/entities/appointment.dart';
import 'package:adsus_mobile/features/appointment_scheduling/domain/entities/appointment_summary.dart';
import 'package:adsus_mobile/features/appointment_scheduling/domain/repositories/appointment_repository.dart';
import 'package:adsus_mobile/features/appointment_scheduling/presentation/viewmodels/my_appointments_view_model.dart';
import 'package:adsus_mobile/shared/providers/app_providers.dart';

class _MockAppointmentRepo extends Mock implements AppointmentRepository {}

class _FakeAuthViewModel extends StateNotifier<AuthState> implements AuthViewModel {
  _FakeAuthViewModel() : super(const AuthState());

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

void main() {
  late _MockAppointmentRepo mockRepo;

  setUp(() {
    mockRepo = _MockAppointmentRepo();
    when(() => mockRepo.listMyAppointments()).thenAnswer((_) async => []);
  });

  ProviderContainer createContainer({List<Override> overrides = const []}) {
    final container = ProviderContainer(
      overrides: [
        appointmentRepositoryProvider.overrideWithValue(mockRepo),
        authViewModelProvider.overrideWith((ref) => _FakeAuthViewModel()),
        ...overrides,
      ],
    );
    addTearDown(container.dispose);
    return container;
  }

  final sampleSelfSummary = AppointmentSummary(
    id: 'appt-self-1',
    slotId: 'slot-1',
    patientProfileId: 'profile-self',
    patientFullName: 'Tôi Là Bệnh Nhân',
    patientPhone: '0912345678',
    status: AppointmentStatus.booked,
    reason: 'Đau đầu',
    createdAt: DateTime(2026, 9, 10, 8, 0),
    slotDate: DateTime(2026, 9, 15),
    startTime: '08:30',
    endTime: '09:00',
    doctorId: 'doc-1',
    doctorName: 'BS. Nguyễn Văn A',
    isBookedForOthers: false,
  );

  final sampleRelativeSummary = AppointmentSummary(
    id: 'appt-rel-1',
    slotId: 'slot-2',
    patientProfileId: 'profile-mother',
    patientFullName: 'Mẹ Nguyễn Thị B',
    patientPhone: '0987654321',
    status: AppointmentStatus.booked,
    reason: 'Đau khớp gối',
    createdAt: DateTime(2026, 9, 10, 9, 0),
    slotDate: DateTime(2026, 9, 16),
    startTime: '10:00',
    endTime: '10:30',
    doctorId: 'doc-2',
    doctorName: 'BS. Lê Thị B',
    isBookedForOthers: true,
    relationshipLabel: 'Mẹ',
    bookedByUserName: 'Con Trai',
  );

  group('MyAppointmentsViewModel Tests', () {
    test('load() populates appointments and computed 2-tab getters correctly', () async {
      when(() => mockRepo.listMyAppointments())
          .thenAnswer((_) async => [sampleSelfSummary, sampleRelativeSummary]);

      final container = createContainer();
      final vm = container.read(myAppointmentsViewModelProvider.notifier);

      // Wait for build's microtask load
      await vm.load();

      final state = container.read(myAppointmentsViewModelProvider);
      expect(state.isLoading, false);
      expect(state.appointments.length, 2);

      // Computed counts
      expect(state.selfCount, 1);
      expect(state.relativeCount, 1);

      // Default scope is 'SELF'
      expect(state.filterScope, 'SELF');
      expect(state.filteredAppointments.length, 1);
      expect(state.filteredAppointments.first.id, 'appt-self-1');
      expect(state.filteredAppointments.first.isBookedForOthers, false);

      // Change scope to 'RELATIVE'
      vm.setFilterScope('RELATIVE');
      final relState = container.read(myAppointmentsViewModelProvider);
      expect(relState.filterScope, 'RELATIVE');
      expect(relState.filteredAppointments.length, 1);
      expect(relState.filteredAppointments.first.id, 'appt-rel-1');
      expect(relState.filteredAppointments.first.isBookedForOthers, true);
      expect(relState.filteredAppointments.first.relationshipLabel, 'Mẹ');
    });

    test('checkCancellationStatus() returns CancellationStatusTodayDto', () async {
      final mockStatus = CancellationStatusTodayDto(
        cancellationsToday: 2,
        maxCancellations: 3,
        canBookOnline: true,
        isNextCancellationFinal: true,
      );
      when(() => mockRepo.getCancellationStatusToday())
          .thenAnswer((_) async => mockStatus);

      final container = createContainer();
      final vm = container.read(myAppointmentsViewModelProvider.notifier);

      final status = await vm.checkCancellationStatus();
      expect(status, isNotNull);
      expect(status!.cancellationsToday, 2);
      expect(status.isNextCancellationFinal, true);
      expect(status.canBookOnline, true);
    });

    test('checkCancellationStatus() catches network exception and returns null (graceful fallback)', () async {
      when(() => mockRepo.getCancellationStatusToday())
          .thenThrow(const ApiException('Server error', statusCode: 500));

      final container = createContainer();
      final vm = container.read(myAppointmentsViewModelProvider.notifier);

      final status = await vm.checkCancellationStatus();
      expect(status, isNull);
    });

    test('cancel() updates appointment status and sets cancelledId', () async {
      when(() => mockRepo.listMyAppointments())
          .thenAnswer((_) async => [sampleSelfSummary]);

      final cancelledAppt = Appointment(
        id: 'appt-self-1',
        slotId: 'slot-1',
        patientProfileId: 'profile-self',
        patientFullName: 'Tôi Là Bệnh Nhân',
        status: AppointmentStatus.cancelled,
        cancelledReason: 'Bận việc',
        createdAt: DateTime(2026, 9, 10, 8, 0),
        updatedAt: DateTime(2026, 9, 11, 8, 0),
      );

      when(() => mockRepo.cancelMyAppointment(
            id: 'appt-self-1',
            cancellationReason: 'Bận việc',
          )).thenAnswer((_) async => cancelledAppt);

      final container = createContainer();
      final vm = container.read(myAppointmentsViewModelProvider.notifier);
      await vm.load();

      await vm.cancel(id: 'appt-self-1', reason: 'Bận việc');

      final state = container.read(myAppointmentsViewModelProvider);
      expect(state.cancelledId, 'appt-self-1');
      expect(state.isMutating, false);
      expect(state.appointments.first.isBooked, false);
      expect(state.appointments.first.cancelledReason, 'Bận việc');
    });

    test('reschedule() retains relative info and patientFullName in state', () async {
      when(() => mockRepo.listMyAppointments())
          .thenAnswer((_) async => [sampleRelativeSummary]);

      final cancelledRelativeAppt = Appointment(
        id: 'appt-rel-1',
        slotId: 'slot-2',
        patientProfileId: 'profile-mother',
        patientFullName: 'Mẹ Nguyễn Thị B',
        status: AppointmentStatus.cancelled,
        cancelledReason: 'Reschedule',
        isBookedForOthers: true,
        relationshipLabel: 'Mẹ',
        bookedByUserName: 'Con Trai',
        createdAt: DateTime(2026, 9, 10, 9, 0),
        updatedAt: DateTime(2026, 9, 11, 9, 0),
      );

      when(() => mockRepo.cancelMyAppointment(
            id: 'appt-rel-1',
            cancellationReason: 'Reschedule',
          )).thenAnswer((_) async => cancelledRelativeAppt);

      final container = createContainer();
      final vm = container.read(myAppointmentsViewModelProvider.notifier);
      await vm.load();

      final currentRelativeAppt = container.read(myAppointmentsViewModelProvider).appointments.first;
      final success = await vm.reschedule(currentRelativeAppt);

      expect(success, true);
      final state = container.read(myAppointmentsViewModelProvider);
      expect(state.cancelledId, 'appt-rel-1');
      expect(state.appointments.first.isBookedForOthers, true);
      expect(state.appointments.first.patientFullName, 'Mẹ Nguyễn Thị B');
      expect(state.appointments.first.relationshipLabel, 'Mẹ');
    });
  });
}
