import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:adsus_mobile/core/network/api_exception.dart';
import 'package:adsus_mobile/features/appointment_scheduling/domain/repositories/appointment_repository.dart';
import 'package:adsus_mobile/features/appointment_scheduling/domain/repositories/symptom_repository.dart';
import 'package:adsus_mobile/features/appointment_scheduling/domain/entities/schedule_slot.dart';
import 'package:adsus_mobile/features/appointment_scheduling/domain/entities/appointment.dart';
import 'package:adsus_mobile/features/appointment_scheduling/domain/entities/symptom.dart';
import 'package:adsus_mobile/features/appointment_scheduling/presentation/viewmodels/book_appointment_view_model.dart';
import 'package:adsus_mobile/shared/providers/app_providers.dart';

class _MockAppointmentRepo extends Mock implements AppointmentRepository {}

class _MockSymptomRepo extends Mock implements SymptomRepository {}

void main() {
  late _MockAppointmentRepo mockRepo;
  late _MockSymptomRepo mockSymptomRepo;
  late ProviderContainer container;

  final maleDoctor = _makeSlot(
    id: 'slot-m1',
    doctorId: 'doc-m',
    doctorName: 'Nam Doctor',
    gender: DoctorGender.male,
  );
  final femaleDoctor = _makeSlot(
    id: 'slot-f1',
    doctorId: 'doc-f',
    doctorName: 'Nữ Doctor',
    gender: DoctorGender.female,
  );
  final maleDoctor2 = _makeSlot(
    id: 'slot-m2',
    doctorId: 'doc-m2',
    doctorName: 'Male Doctor 2',
    gender: DoctorGender.male,
  );

  setUp(() {
    mockRepo = _MockAppointmentRepo();
    mockSymptomRepo = _MockSymptomRepo();
    when(() => mockRepo.searchOpenSlots())
        .thenAnswer((_) async => [maleDoctor, femaleDoctor, maleDoctor2]);
    container = ProviderContainer(
      overrides: [
        appointmentRepositoryProvider.overrideWithValue(mockRepo),
        symptomRepositoryProvider.overrideWithValue(mockSymptomRepo),
      ],
    );
  });

  tearDown(() => container.dispose());

  /// Helper: đợi loadSlots xong (Future.microtask trong build chạy sau read đầu tiên)
  Future<void> loadAndWait() async {
    await container
        .read(bookAppointmentViewModelProvider.notifier)
        .loadSlots();
    await Future.value(); // flush microtask queue
  }

  group('filteredDoctorOptions — getter', () {
    test('khong filter → tra ve tat ca bac si', () async {
      await loadAndWait();

      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.doctorOptions.length, 3);
      expect(state.filteredDoctorOptions.length, 3);
    });

    test('filter male → chi tra ve bac si nam', () async {
      await loadAndWait();

      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDoctorGender(DoctorGender.male);

      await Future.value();
      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.filteredDoctorOptions.length, 2);
      expect(
        state.filteredDoctorOptions.every((d) => d.gender == DoctorGender.male),
        isTrue,
      );
    });

    test('filter female → chi tra ve bac si nu', () async {
      await loadAndWait();

      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDoctorGender(DoctorGender.female);

      await Future.value();
      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.filteredDoctorOptions.length, 1);
      expect(state.filteredDoctorOptions.first.gender, DoctorGender.female);
    });

    test('reset ve null → tra ve tat ca bac si (BAI-01)', () async {
      await loadAndWait();

      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDoctorGender(DoctorGender.male);

      await Future.value();
      expect(
        container.read(bookAppointmentViewModelProvider).filteredDoctorOptions.length,
        2,
      );

      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDoctorGender(null);

      await Future.value();
      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.filteredDoctorOptions.length, 3);
    });
  });

  group('selectDoctorGender', () {
    test('chon gender → reset selectedDoctorId', () async {
      await loadAndWait();

      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDoctor('doc-m');

      expect(
        container.read(bookAppointmentViewModelProvider).selectedDoctorId,
        'doc-m',
      );

      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDoctorGender(DoctorGender.female);

      await Future.value();
      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.selectedDoctorGender, DoctorGender.female);
      expect(state.selectedDoctorId, isNull,
          reason: 'Chon gender phai reset selectedDoctorId');
    });

    test('reset ve null → reset selectedDoctorId (BAI-01)', () async {
      await loadAndWait();

      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDoctorGender(DoctorGender.male);
      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDoctor('doc-m');

      expect(
        container.read(bookAppointmentViewModelProvider).selectedDoctorId,
        'doc-m',
      );

      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDoctorGender(null);

      await Future.value();
      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.selectedDoctorId, isNull,
          reason: 'Reset gender ve null phai reset selectedDoctorId');
    });

    test('chon gender → reset selectedSlotId', () async {
      await loadAndWait();

      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDoctorGender(DoctorGender.male);
      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDoctor('doc-m');
      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDate(maleDoctor.slotDate);
      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectSlot('slot-m1');

      expect(
        container.read(bookAppointmentViewModelProvider).selectedSlotId,
        'slot-m1',
      );

      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDoctorGender(DoctorGender.female);

      await Future.value();
      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.selectedSlotId, isNull,
          reason: 'Chon gender phai reset selectedSlotId');
      expect(state.selectedDate, isNull,
          reason: 'Chon gender phai reset selectedDate');
    });
  });

  group('resetForNewBooking', () {
    test('resetForNewBooking → xoa selectedDoctorId, selectedSlotId, selectedDate', () async {
      await loadAndWait();

      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDoctorGender(DoctorGender.male);
      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDoctor('doc-m');
      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDate(maleDoctor.slotDate);
      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectSlot('slot-m1');

      container
          .read(bookAppointmentViewModelProvider.notifier)
          .resetForNewBooking();

      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.selectedDoctorId, isNull);
      expect(state.selectedSlotId, isNull);
      expect(state.selectedDate, isNull);
      expect(state.selectedDoctorGender, isNull);
      expect(state.reason, isEmpty);
    });

    test('resetForNewBooking giu nguyen slots', () async {
      await loadAndWait();

      final slotsBefore =
          container.read(bookAppointmentViewModelProvider).slots;

      container
          .read(bookAppointmentViewModelProvider.notifier)
          .resetForNewBooking();

      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.slots, equals(slotsBefore));
      expect(state.slots.length, 3);
    });
  });

  group('DoctorOption', () {
    test('equality and hashCode are based on id', () {
      const doc1 = DoctorOption(
        id: 'doc-1',
        name: 'Dr. One',
        status: DoctorStatus.active,
        gender: DoctorGender.male,
      );
      const doc1SameId = DoctorOption(
        id: 'doc-1',
        name: 'Dr. One Changed Name',
        status: DoctorStatus.inactive,
        gender: DoctorGender.female,
      );
      const doc2 = DoctorOption(
        id: 'doc-2',
        name: 'Dr. Two',
        status: DoctorStatus.active,
        gender: DoctorGender.male,
      );

      expect(doc1 == doc1SameId, isTrue);
      expect(doc1.hashCode, equals(doc1SameId.hashCode));
      expect(doc1 == doc2, isFalse);
      expect(doc1 == doc1, isTrue);
      expect(doc1 == Object(), isFalse);
    });

    test('toString returns name', () {
      const doc = DoctorOption(
        id: 'doc-1',
        name: 'Dr. John Doe',
        status: DoctorStatus.active,
      );
      expect(doc.toString(), equals('Dr. John Doe'));
    });
  });

  group('selectDoctor', () {
    test('selectDoctor clears selectedSlotId', () async {
      await loadAndWait();
      final vm = container.read(bookAppointmentViewModelProvider.notifier);

      vm.selectDoctor('doc-m');
      vm.selectDate(maleDoctor.slotDate);
      vm.selectSlot('slot-m1');
      expect(container.read(bookAppointmentViewModelProvider).selectedSlotId, 'slot-m1');

      vm.selectDoctor('doc-m2');
      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.selectedDoctorId, 'doc-m2');
      expect(state.selectedSlotId, isNull, reason: 'Doi bac si phai clear selectedSlotId');
    });

    test('selectDoctor with same doctorId is a no-op (early return)', () async {
      await loadAndWait();
      final vm = container.read(bookAppointmentViewModelProvider.notifier);

      vm.selectDoctor('doc-m');
      vm.selectDate(maleDoctor.slotDate);
      vm.selectSlot('slot-m1');
      expect(container.read(bookAppointmentViewModelProvider).selectedSlotId, 'slot-m1');

      vm.selectDoctor('doc-m'); // same doctor
      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.selectedDoctorId, 'doc-m');
      expect(state.selectedSlotId, 'slot-m1', reason: 'Chon cung bac si khong clear slot');
    });

    test('selectDoctor with null clears both selectedDoctorId and selectedSlotId', () async {
      await loadAndWait();
      final vm = container.read(bookAppointmentViewModelProvider.notifier);

      vm.selectDoctor('doc-m');
      vm.selectSlot('slot-m1');

      vm.selectDoctor(null);
      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.selectedDoctorId, isNull);
      expect(state.selectedSlotId, isNull);
    });
  });

  group('selectDate', () {
    test('selectDate clears selectedSlotId', () async {
      await loadAndWait();
      final vm = container.read(bookAppointmentViewModelProvider.notifier);

      vm.selectDoctor('doc-m');
      vm.selectDate(maleDoctor.slotDate);
      vm.selectSlot('slot-m1');
      expect(container.read(bookAppointmentViewModelProvider).selectedSlotId, 'slot-m1');

      final newDate = maleDoctor.slotDate.add(const Duration(days: 1));
      vm.selectDate(newDate);
      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.selectedDate, equals(newDate));
      expect(state.selectedSlotId, isNull, reason: 'Doi ngay phai clear selectedSlotId');
    });

    test('selectDate with same date is a no-op (early return)', () async {
      await loadAndWait();
      final vm = container.read(bookAppointmentViewModelProvider.notifier);

      vm.selectDate(maleDoctor.slotDate);
      vm.selectSlot('slot-m1');

      vm.selectDate(maleDoctor.slotDate); // same date
      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.selectedDate, equals(maleDoctor.slotDate));
      expect(state.selectedSlotId, 'slot-m1', reason: 'Chon cung ngay khong clear slot');
    });

    test('selectDate with null clears selectedDate and selectedSlotId', () async {
      await loadAndWait();
      final vm = container.read(bookAppointmentViewModelProvider.notifier);

      vm.selectDate(maleDoctor.slotDate);
      vm.selectSlot('slot-m1');

      vm.selectDate(null);
      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.selectedDate, isNull);
      expect(state.selectedSlotId, isNull);
    });
  });

  group('selectWeek', () {
    test('selectWeek clears selectedDate and selectedSlotId', () async {
      await loadAndWait();
      final vm = container.read(bookAppointmentViewModelProvider.notifier);

      vm.selectDate(maleDoctor.slotDate);
      vm.selectSlot('slot-m1');
      expect(container.read(bookAppointmentViewModelProvider).selectedDate, isNotNull);
      expect(container.read(bookAppointmentViewModelProvider).selectedSlotId, 'slot-m1');

      vm.selectWeek(1);
      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.selectedWeekIndex, 1);
      expect(state.selectedDate, isNull, reason: 'Doi tuan phai clear selectedDate');
      expect(state.selectedSlotId, isNull, reason: 'Doi tuan phai clear selectedSlotId');
    });

    test('selectWeek with same index is a no-op (early return)', () async {
      await loadAndWait();
      final vm = container.read(bookAppointmentViewModelProvider.notifier);

      vm.selectWeek(2);
      vm.selectDate(maleDoctor.slotDate);
      vm.selectSlot('slot-m1');

      vm.selectWeek(2); // same index
      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.selectedWeekIndex, 2);
      expect(state.selectedDate, equals(maleDoctor.slotDate));
      expect(state.selectedSlotId, 'slot-m1');
    });
  });

  group('resetScreenState and resetForNewBooking retain symptomCategories', () {
    test('resetScreenState retains symptomCategories and clears blocks and expanded state', () async {
      const sampleCategories = [
        SymptomCategory(id: 'cat-1', name: 'Đau đầu'),
        SymptomCategory(id: 'cat-2', name: 'Sốt'),
      ];
      when(() => mockSymptomRepo.getCategories()).thenAnswer((_) async => sampleCategories);

      await loadAndWait();
      final vm = container.read(bookAppointmentViewModelProvider.notifier);

      await vm.loadSymptomCategories();
      vm.toggleSymptomSection();
      vm.addSymptomBlock();
      vm.selectDoctor('doc-m');
      vm.selectSlot('slot-m1');

      var state = container.read(bookAppointmentViewModelProvider);
      expect(state.symptomCategories.length, 2);
      expect(state.symptomBlocks.length, 1);
      expect(state.isSymptomSectionExpanded, isTrue);
      expect(state.selectedDoctorId, 'doc-m');
      expect(state.selectedSlotId, 'slot-m1');

      vm.resetScreenState();

      state = container.read(bookAppointmentViewModelProvider);
      expect(state.symptomCategories.length, 2, reason: 'symptomCategories phai duoc giu lai');
      expect(state.symptomCategories, equals(sampleCategories));
      expect(state.symptomBlocks, isEmpty, reason: 'symptomBlocks phai duoc reset');
      expect(state.isSymptomSectionExpanded, isFalse);
      expect(state.selectedDoctorId, isNull);
      expect(state.selectedSlotId, isNull);
    });

    test('resetForNewBooking retains symptomCategories and clears blocks and expanded state', () async {
      const sampleCategories = [
        SymptomCategory(id: 'cat-1', name: 'Đau đầu'),
      ];
      when(() => mockSymptomRepo.getCategories()).thenAnswer((_) async => sampleCategories);

      await loadAndWait();
      final vm = container.read(bookAppointmentViewModelProvider.notifier);

      await vm.loadSymptomCategories();
      vm.toggleSymptomSection();
      vm.addSymptomBlock();
      vm.selectDoctor('doc-m');
      vm.selectSlot('slot-m1');

      vm.resetForNewBooking();

      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.symptomCategories.length, 1, reason: 'symptomCategories phai duoc giu lai');
      expect(state.symptomCategories, equals(sampleCategories));
      expect(state.symptomBlocks, isEmpty, reason: 'symptomBlocks phai duoc reset');
      expect(state.isSymptomSectionExpanded, isFalse);
      expect(state.selectedDoctorId, isNull);
      expect(state.selectedSlotId, isNull);
    });
  });

  group('visibleSlots', () {
    test('chua chon bac si → tra ve rong', () async {
      await loadAndWait();

      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.visibleSlots, isEmpty);
    });

    test('chon bac si + ngay → tra ve slot phu hop', () async {
      await loadAndWait();

      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDoctor('doc-m');
      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDate(maleDoctor.slotDate);

      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.visibleSlots.length, 1);
      expect(state.visibleSlots.first.id, 'slot-m1');
    });

    test('filter gender male → visibleSlots chi tu bac si nam', () async {
      await loadAndWait();

      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDoctorGender(DoctorGender.male);
      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDoctor('doc-m');
      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDate(maleDoctor.slotDate);

      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.visibleSlots.length, 1);
      expect(state.visibleSlots.first.doctorGender, DoctorGender.male);
    });
  });

  group('book()', () {
    test('book thanh cong → xoa slot khoi danh sach', () async {
      when(() => mockRepo.bookAppointment(
            scheduleSlotId: any(named: 'scheduleSlotId'),
            reason: any(named: 'reason'),
            symptoms: any(named: 'symptoms'),
          )).thenAnswer((_) async => _makeAppointment(
            id: 'ap-123',
            slotId: 'slot-m1',
          ));

      await loadAndWait();

      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDoctor('doc-m');
      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDate(maleDoctor.slotDate);
      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectSlot('slot-m1');

      // book() is async — await its Future so the continuation (copyWith with bookingSuccess)
      // has a chance to run before we read state
      await container.read(bookAppointmentViewModelProvider.notifier).book(reason: 'Kham benh');

      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.bookingSuccess, 'ap-123');
      expect(state.slots.length, 2,
          reason: 'Slot da dat phai duoc xoa khoi danh sach');
      expect(state.slots.any((s) => s.id == 'slot-m1'), isFalse);
      expect(state.selectedSlotId, isNull,
          reason: 'selectedSlotId phai reset sau khi dat thanh cong');
    });

    test('book that bai → khong xoa slot', () async {
      when(() => mockRepo.bookAppointment(
            scheduleSlotId: any(named: 'scheduleSlotId'),
            reason: any(named: 'reason'),
            symptoms: any(named: 'symptoms'),
          )).thenThrow(ApiException('Server error', statusCode: 500));

      await loadAndWait();

      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDoctor('doc-m');
      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectDate(maleDoctor.slotDate);
      container
          .read(bookAppointmentViewModelProvider.notifier)
          .selectSlot('slot-m1');

      container.read(bookAppointmentViewModelProvider.notifier).book(reason: 'Kham benh');
      await Future.delayed(Duration.zero);

      final state = container.read(bookAppointmentViewModelProvider);
      expect(state.slots.length, 3,
          reason: 'Slot khong duoc xoa khi book that bai');
      expect(state.bookingSuccess, isNull);
    });
  });
}

// --- Helpers ---

ScheduleSlot _makeSlot({
  required String id,
  required String doctorId,
  required String doctorName,
  required DoctorGender gender,
}) {
  // Dùng ngày tương lai để test ổn định (tránh hardcoded date có thể đã qua)
  final slotDate = DateTime.now().add(const Duration(days: 7));
  return ScheduleSlot(
    id: id,
    doctorId: doctorId,
    doctorName: doctorName,
    slotDate: slotDate,
    startTime: '08:00',
    endTime: '08:30',
    status: SlotStatus.open,
    doctorStatus: DoctorStatus.active,
    doctorGender: gender,
  );
}

Appointment _makeAppointment({
  required String id,
  required String slotId,
}) {
  return Appointment(
    id: id,
    slotId: slotId,
    patientProfileId: 'patient-1',
    status: AppointmentStatus.booked,
    createdAt: DateTime.now(),
    updatedAt: DateTime.now(),
  );
}
