import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:adsus_mobile/features/appointment_scheduling/data/dtos/symptom_dtos.dart';
import 'package:adsus_mobile/features/appointment_scheduling/domain/entities/appointment.dart';
import 'package:adsus_mobile/features/appointment_scheduling/domain/entities/schedule_slot.dart';
import 'package:adsus_mobile/features/appointment_scheduling/domain/repositories/appointment_repository.dart';
import 'package:adsus_mobile/features/appointment_scheduling/domain/repositories/symptom_repository.dart';
import 'package:adsus_mobile/features/appointment_scheduling/presentation/viewmodels/book_appointment_view_model.dart';
import 'package:adsus_mobile/features/appointment_scheduling/presentation/views/book_appointment_screen.dart';
import 'package:adsus_mobile/features/appointment_scheduling/presentation/views/widgets/edit_clinical_info_sheet.dart';
import 'package:adsus_mobile/features/patient_relationship/domain/entities/patient_relationship.dart';
import 'package:adsus_mobile/features/patient_relationship/domain/repositories/patient_relationship_repository.dart';
import 'package:adsus_mobile/features/patient_relationship/presentation/viewmodels/my_relatives_view_model.dart';
import 'package:adsus_mobile/features/patient_relationship/presentation/views/edit_relative_screen.dart';
import 'package:adsus_mobile/shared/providers/app_providers.dart';

class _MockPatientRelationshipRepo extends Mock
    implements PatientRelationshipRepository {}

class _MockAppointmentRepo extends Mock implements AppointmentRepository {}

class _MockSymptomRepo extends Mock implements SymptomRepository {}

void main() {
  setUpAll(() {
    registerFallbackValue(DateTime.now());
    registerFallbackValue(<SymptomInput>[]);
  });

  group('CHALLENGER 2: Mobile Edit Relative Flow & Edge Cases', () {
    late _MockPatientRelationshipRepo mockRepo;

    final initialRelative = PatientRelationship(
      relationshipId: 'rel-adv-01',
      patientProfileId: 'patient-adv-01',
      fullName: 'Nguyễn Văn Test',
      phone: '0912345678',
      relationshipName: 'Anh trai',
      dateOfBirth: DateTime(1990, 5, 10),
      createdAt: DateTime(2026, 1, 1),
    );

    setUp(() {
      mockRepo = _MockPatientRelationshipRepo();
      when(() => mockRepo.getRelatives()).thenAnswer((_) async => [initialRelative]);
    });

    Widget buildEditWidget(PatientRelationship relative) {
      return ProviderScope(
        overrides: [
          patientRelationshipRepositoryProvider.overrideWithValue(mockRepo),
        ],
        child: MaterialApp(
          home: EditRelativeScreen(relative: relative),
        ),
      );
    }

    testWidgets('ADV-REL-01: Empty phone number (optional) is allowed and passes phone: null',
        (tester) async {
      final relativeWithoutPhone = PatientRelationship(
        relationshipId: 'rel-no-phone',
        patientProfileId: 'patient-adv-01',
        fullName: 'Bà Ngoại',
        phone: null,
        relationshipName: 'Bà ngoại',
        dateOfBirth: DateTime(1950, 1, 1),
        createdAt: DateTime(2026, 1, 1),
      );

      when(() => mockRepo.updateRelative(
            'rel-no-phone',
            fullName: 'Bà Ngoại',
            phone: null,
            dateOfBirth: any(named: 'dateOfBirth'),
            relationshipName: 'Bà ngoại',
          )).thenAnswer((_) async => relativeWithoutPhone);

      await tester.pumpWidget(buildEditWidget(relativeWithoutPhone));
      await tester.pumpAndSettle();

      // Tap submit
      await tester.tap(find.text('CẬP NHẬT THÔNG TIN'));
      await tester.pumpAndSettle();

      // Verify updateRelative was called with phone: null and no validation error appeared
      expect(find.text('Số điện thoại không hợp lệ'), findsNothing);
      verify(() => mockRepo.updateRelative(
            'rel-no-phone',
            fullName: 'Bà Ngoại',
            phone: null,
            dateOfBirth: any(named: 'dateOfBirth'),
            relationshipName: 'Bà ngoại',
          )).called(1);
    });

    testWidgets('ADV-REL-02: Clearing existing phone number to empty passes phone: null',
        (tester) async {
      when(() => mockRepo.updateRelative(
            'rel-adv-01',
            fullName: 'Nguyễn Văn Test',
            phone: null,
            dateOfBirth: any(named: 'dateOfBirth'),
            relationshipName: 'Anh trai',
          )).thenAnswer((_) async => initialRelative);

      await tester.pumpWidget(buildEditWidget(initialRelative));
      await tester.pumpAndSettle();

      // Clear phone field (field index 1)
      final phoneField = find.byType(TextFormField).at(1);
      await tester.enterText(phoneField, '');
      await tester.pumpAndSettle();

      // Tap submit
      await tester.tap(find.text('CẬP NHẬT THÔNG TIN'));
      await tester.pumpAndSettle();

      expect(find.text('Số điện thoại không hợp lệ'), findsNothing);
      verify(() => mockRepo.updateRelative(
            'rel-adv-01',
            fullName: 'Nguyễn Văn Test',
            phone: null,
            dateOfBirth: any(named: 'dateOfBirth'),
            relationshipName: 'Anh trai',
          )).called(1);
    });

    testWidgets('ADV-REL-03: Invalid phone numbers are rejected and block submission',
        (tester) async {
      await tester.pumpWidget(buildEditWidget(initialRelative));
      await tester.pumpAndSettle();

      final phoneField = find.byType(TextFormField).at(1);

      // Test short number
      await tester.enterText(phoneField, '12345');
      await tester.tap(find.text('CẬP NHẬT THÔNG TIN'));
      await tester.pumpAndSettle();
      expect(find.text('Số điện thoại không hợp lệ'), findsOneWidget);

      // Test 11-digit number
      await tester.enterText(phoneField, '09123456789');
      await tester.tap(find.text('CẬP NHẬT THÔNG TIN'));
      await tester.pumpAndSettle();
      expect(find.text('Số điện thoại không hợp lệ'), findsOneWidget);

      // Test alphanumeric
      await tester.enterText(phoneField, '091234567a');
      await tester.tap(find.text('CẬP NHẬT THÔNG TIN'));
      await tester.pumpAndSettle();
      expect(find.text('Số điện thoại không hợp lệ'), findsOneWidget);

      // Test starts with 84
      await tester.enterText(phoneField, '8491234567');
      await tester.tap(find.text('CẬP NHẬT THÔNG TIN'));
      await tester.pumpAndSettle();
      expect(find.text('Số điện thoại không hợp lệ'), findsOneWidget);

      // Verify updateRelative was NEVER called
      verifyNever(() => mockRepo.updateRelative(any(),
          fullName: any(named: 'fullName'),
          phone: any(named: 'phone'),
          dateOfBirth: any(named: 'dateOfBirth'),
          relationshipName: any(named: 'relationshipName')));
    });

    testWidgets('ADV-REL-04: HTML tags in relationship name are rejected and block submission',
        (tester) async {
      await tester.pumpWidget(buildEditWidget(initialRelative));
      await tester.pumpAndSettle();

      final relField = find.byType(TextFormField).at(2);

      // Test multiple HTML vectors
      final badInputs = [
        '<b>Mẹ</b>',
        '<script>alert(1)</script>',
        '<div>Cha</div>',
        '<img src=x onerror=alert(1)>',
        '<a href="test">Chị</a>',
      ];

      for (final badInput in badInputs) {
        await tester.enterText(relField, badInput);
        await tester.tap(find.text('CẬP NHẬT THÔNG TIN'));
        await tester.pumpAndSettle();

        expect(find.text('Nhãn quan hệ không được chứa thẻ HTML'), findsOneWidget);
      }

      // Verify updateRelative was NEVER called
      verifyNever(() => mockRepo.updateRelative(any(),
          fullName: any(named: 'fullName'),
          phone: any(named: 'phone'),
          dateOfBirth: any(named: 'dateOfBirth'),
          relationshipName: any(named: 'relationshipName')));
    });

    testWidgets('ADV-REL-05: Clinical inequalities in relationship name are permitted',
        (tester) async {
      final updated = PatientRelationship(
        relationshipId: 'rel-adv-01',
        patientProfileId: 'patient-adv-01',
        fullName: 'Nguyễn Văn Test',
        phone: '0912345678',
        relationshipName: 'Bé < 5 tuổi',
        dateOfBirth: DateTime(1990, 5, 10),
        createdAt: DateTime(2026, 1, 1),
      );

      when(() => mockRepo.updateRelative(
            'rel-adv-01',
            fullName: 'Nguyễn Văn Test',
            phone: '0912345678',
            dateOfBirth: any(named: 'dateOfBirth'),
            relationshipName: 'Bé < 5 tuổi',
          )).thenAnswer((_) async => updated);

      await tester.pumpWidget(buildEditWidget(initialRelative));
      await tester.pumpAndSettle();

      final relField = find.byType(TextFormField).at(2);
      await tester.enterText(relField, 'Bé < 5 tuổi');
      await tester.tap(find.text('CẬP NHẬT THÔNG TIN'));
      await tester.pumpAndSettle();

      expect(find.text('Nhãn quan hệ không được chứa thẻ HTML'), findsNothing);
      verify(() => mockRepo.updateRelative(
            'rel-adv-01',
            fullName: 'Nguyễn Văn Test',
            phone: '0912345678',
            dateOfBirth: any(named: 'dateOfBirth'),
            relationshipName: 'Bé < 5 tuổi',
          )).called(1);
    });
  });

  group('CHALLENGER 2: updateRelativeLocally State Integrity Suite', () {
    test('ADV-STATE-01: Correctly updates targeted relative while strictly preserving order & others', () {
      final container = ProviderContainer();
      addTearDown(container.dispose);

      final r0 = PatientRelationship(
        relationshipId: 'rel-0',
        patientProfileId: 'p-0',
        fullName: 'Người thân 0',
        phone: '0900000000',
        createdAt: DateTime(2026, 1, 1),
      );
      final r1 = PatientRelationship(
        relationshipId: 'rel-1',
        patientProfileId: 'p-1',
        fullName: 'Người thân 1',
        phone: '0900000001',
        relationshipName: 'Anh',
        createdAt: DateTime(2026, 1, 2),
      );
      final r2 = PatientRelationship(
        relationshipId: 'rel-2',
        patientProfileId: 'p-2',
        fullName: 'Người thân 2',
        phone: '0900000002',
        createdAt: DateTime(2026, 1, 3),
      );
      final r3 = PatientRelationship(
        relationshipId: 'rel-3',
        patientProfileId: 'p-3',
        fullName: 'Người thân 3',
        phone: '0900000003',
        createdAt: DateTime(2026, 1, 4),
      );

      final notifier = container.read(myRelativesViewModelProvider.notifier);
      notifier.state = MyRelativesState(
        relatives: [r0, r1, r2, r3],
        isLoading: false,
      );

      expect(container.read(myRelativesViewModelProvider).relatives.length, 4);

      // Perform local update on r1
      final updatedR1 = PatientRelationship(
        relationshipId: 'rel-1',
        patientProfileId: 'p-1',
        fullName: 'Người thân 1 (Đã đổi tên)',
        phone: '0909999999',
        relationshipName: 'Anh hai',
        createdAt: DateTime(2026, 1, 2),
      );

      notifier.updateRelativeLocally(updatedR1);

      final currentList = container.read(myRelativesViewModelProvider).relatives;
      // 1. Length preserved
      expect(currentList.length, 4);
      // 2. Exact elements and positions
      expect(currentList[0], equals(r0));
      expect(currentList[1].fullName, 'Người thân 1 (Đã đổi tên)');
      expect(currentList[1].phone, '0909999999');
      expect(currentList[1].relationshipName, 'Anh hai');
      expect(currentList[2], equals(r2));
      expect(currentList[3], equals(r3));
    });

    test('ADV-STATE-02: Non-existent relative ID does not alter state or crash', () {
      final container = ProviderContainer();
      addTearDown(container.dispose);

      final r0 = PatientRelationship(
        relationshipId: 'rel-0',
        patientProfileId: 'p-0',
        fullName: 'Người thân 0',
        phone: '0900000000',
        createdAt: DateTime(2026, 1, 1),
      );

      final notifier = container.read(myRelativesViewModelProvider.notifier);
      notifier.state = MyRelativesState(
        relatives: [r0],
        isLoading: false,
      );

      final ghostRelative = PatientRelationship(
        relationshipId: 'rel-ghost',
        patientProfileId: 'p-ghost',
        fullName: 'Người lạ',
        createdAt: DateTime(2026, 1, 1),
      );

      // Must not throw and must not modify relatives
      notifier.updateRelativeLocally(ghostRelative);

      final currentList = container.read(myRelativesViewModelProvider).relatives;
      expect(currentList.length, 1);
      expect(currentList.first, equals(r0));
    });
  });

  group('CHALLENGER 2: Mobile Form Blocking (book_appointment & edit_clinical_info)', () {
    testWidgets('ADV-BLOCK-01: BookAppointmentScreen blocks book() when reason has HTML tags',
        (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      final mockApptRepo = _MockAppointmentRepo();
      final mockRelRepo = _MockPatientRelationshipRepo();

      final sampleSlot = ScheduleSlot(
        id: 'slot-adv-1',
        doctorId: 'doc-1',
        doctorName: 'BS. Test',
        slotDate: DateTime.now().add(const Duration(days: 1)),
        startTime: '08:00',
        endTime: '09:00',
        status: SlotStatus.open,
      );

      when(() => mockApptRepo.searchOpenSlots()).thenAnswer((_) async => [sampleSlot]);
      when(() => mockRelRepo.getRelatives()).thenAnswer((_) async => []);

      final container = ProviderContainer(
        overrides: [
          appointmentRepositoryProvider.overrideWithValue(mockApptRepo),
          patientRelationshipRepositoryProvider.overrideWithValue(mockRelRepo),
        ],
      );
      addTearDown(container.dispose);

      await tester.pumpWidget(
        UncontrolledProviderScope(
          container: container,
          child: const MaterialApp(
            home: BookAppointmentScreen(),
          ),
        ),
      );
      await tester.pumpAndSettle();

      // Pre-select a slot to enable the confirm button
      container.read(bookAppointmentViewModelProvider.notifier).selectSlot('slot-adv-1');
      await tester.pumpAndSettle();

      // Find reason input field (TextField)
      final reasonField = find.byType(TextField).first;
      expect(reasonField, findsOneWidget);

      await tester.ensureVisible(reasonField);
      await tester.enterText(reasonField, '<b>Đau đầu dữ dội</b><script>alert(1)</script>');
      await tester.pumpAndSettle();

      // Find confirm button
      final confirmBtn = find.text('XÁC NHẬN ĐẶT LỊCH');
      await tester.ensureVisible(confirmBtn);
      await tester.tap(confirmBtn);
      await tester.pumpAndSettle();

      // Verify SnackBar displayed
      expect(find.text('Lý do khám không được chứa thẻ HTML.'), findsOneWidget);

      // Verify book() was NOT called
      final bookVm = container.read(bookAppointmentViewModelProvider);
      expect(bookVm.isBooking, isFalse);
      verifyNever(() => mockApptRepo.bookAppointment(
            scheduleSlotId: any(named: 'scheduleSlotId'),
            reason: any(named: 'reason'),
            symptoms: any(named: 'symptoms'),
            relationshipId: any(named: 'relationshipId'),
          ));
    });

    testWidgets('ADV-BLOCK-02: EditClinicalInfoSheet blocks saving when reason has HTML tags',
        (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      final mockApptRepo = _MockAppointmentRepo();
      final mockSymptomRepo = _MockSymptomRepo();

      when(() => mockSymptomRepo.getCategories()).thenAnswer((_) async => []);

      final appointment = Appointment(
        id: 'appt-adv-1',
        slotId: 'slot-adv-1',
        patientProfileId: 'patient-1',
        patientFullName: 'Bệnh nhân Test',
        doctorName: 'BS. A',
        slotDate: DateTime.now().add(const Duration(days: 2)),
        startTime: '09:00',
        endTime: '10:00',
        status: AppointmentStatus.booked,
        reason: 'Khám định kỳ',
        symptoms: [],
        createdAt: DateTime.now(),
        updatedAt: DateTime.now(),
      );

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            appointmentRepositoryProvider.overrideWithValue(mockApptRepo),
            symptomRepositoryProvider.overrideWithValue(mockSymptomRepo),
          ],
          child: MaterialApp(
            home: Scaffold(
              body: EditClinicalInfoSheet(appointment: appointment),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      // Find reason TextField
      final reasonField = find.byType(TextField).first;
      expect(reasonField, findsOneWidget);

      // Enter HTML
      await tester.enterText(reasonField, '<html><body>Đau lưng</body></html>');
      await tester.pumpAndSettle();

      // Tap save
      final saveBtn = find.text('Lưu thay đổi');
      await tester.tap(saveBtn);
      await tester.pumpAndSettle();

      // Verify error message is displayed
      expect(find.text('Lý do khám không được chứa thẻ HTML.'), findsOneWidget);

      // Verify updateClinicalInfo was NEVER called
      verifyNever(() => mockApptRepo.updateClinicalInfo(
            any(),
            reason: any(named: 'reason'),
            symptoms: any(named: 'symptoms'),
          ));
    });

    testWidgets('ADV-BLOCK-03: EditClinicalInfoSheet permits clinical inequality reasons',
        (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      final mockApptRepo = _MockAppointmentRepo();
      final mockSymptomRepo = _MockSymptomRepo();

      when(() => mockSymptomRepo.getCategories()).thenAnswer((_) async => []);

      final appointment = Appointment(
        id: 'appt-adv-2',
        slotId: 'slot-adv-2',
        patientProfileId: 'patient-1',
        patientFullName: 'Bệnh nhân Test',
        doctorName: 'BS. A',
        slotDate: DateTime.now().add(const Duration(days: 2)),
        startTime: '09:00',
        endTime: '10:00',
        status: AppointmentStatus.booked,
        reason: 'Khám cũ',
        symptoms: [],
        createdAt: DateTime.now(),
        updatedAt: DateTime.now(),
      );

      when(() => mockApptRepo.updateClinicalInfo(
            'appt-adv-2',
            reason: 'Sốt < 38.5°C, ho < 3 ngày',
            symptoms: any(named: 'symptoms'),
          )).thenAnswer((_) async => appointment);

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            appointmentRepositoryProvider.overrideWithValue(mockApptRepo),
            symptomRepositoryProvider.overrideWithValue(mockSymptomRepo),
          ],
          child: MaterialApp(
            home: Scaffold(
              body: EditClinicalInfoSheet(appointment: appointment),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      final reasonField = find.byType(TextField).first;
      await tester.enterText(reasonField, 'Sốt < 38.5°C, ho < 3 ngày');
      await tester.pumpAndSettle();

      final saveBtn = find.text('Lưu thay đổi');
      await tester.tap(saveBtn);
      await tester.pumpAndSettle();

      expect(find.text('Lý do khám không được chứa thẻ HTML.'), findsNothing);
      verify(() => mockApptRepo.updateClinicalInfo(
            'appt-adv-2',
            reason: 'Sốt < 38.5°C, ho < 3 ngày',
            symptoms: any(named: 'symptoms'),
          )).called(1);
    });
  });
}
