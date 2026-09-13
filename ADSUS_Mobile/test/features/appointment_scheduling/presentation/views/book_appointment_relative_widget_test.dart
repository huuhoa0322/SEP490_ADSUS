import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:adsus_mobile/features/appointment_scheduling/domain/entities/schedule_slot.dart';
import 'package:adsus_mobile/features/appointment_scheduling/domain/repositories/appointment_repository.dart';
import 'package:adsus_mobile/features/appointment_scheduling/presentation/views/book_appointment_screen.dart';
import 'package:adsus_mobile/features/patient_relationship/domain/repositories/patient_relationship_repository.dart';
import 'package:adsus_mobile/shared/providers/app_providers.dart';

class _MockAppointmentRepo extends Mock implements AppointmentRepository {}

class _MockPatientRelationshipRepo extends Mock implements PatientRelationshipRepository {}

void main() {
  late _MockAppointmentRepo mockAppointmentRepo;
  late _MockPatientRelationshipRepo mockRelationshipRepo;

  final sampleSlot = ScheduleSlot(
    id: 'slot-1',
    doctorId: 'doc-1',
    doctorName: 'BS. Test',
    slotDate: DateTime.now().add(const Duration(days: 1)),
    startTime: '08:00',
    endTime: '09:00',
    status: SlotStatus.open,
  );

  setUp(() {
    mockAppointmentRepo = _MockAppointmentRepo();
    mockRelationshipRepo = _MockPatientRelationshipRepo();

    when(() => mockAppointmentRepo.searchOpenSlots()).thenAnswer((_) async => [sampleSlot]);
    when(() => mockRelationshipRepo.getRelatives()).thenAnswer((_) async => []);
  });

  Widget buildTestWidget() {
    return ProviderScope(
      overrides: [
        appointmentRepositoryProvider.overrideWithValue(mockAppointmentRepo),
        patientRelationshipRepositoryProvider.overrideWithValue(mockRelationshipRepo),
      ],
      child: const MaterialApp(
        home: BookAppointmentScreen(),
      ),
    );
  }

  group('BookAppointmentScreen - Relative Booking Widget Tests', () {
    testWidgets('TC-FE-15: BookAppointmentScreen_NoInfiniteLoop verifies stable rebuild on relative selection',
        (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(buildTestWidget());
      await tester.pumpAndSettle();

      // Ensure radio tiles are present and scrolled into view
      final relativeRadio = find.text('Người thân');
      await tester.ensureVisible(relativeRadio);
      await tester.pumpAndSettle();

      expect(relativeRadio, findsOneWidget);

      // Switch selection to "Người thân"
      await tester.tap(relativeRadio);
      await tester.pumpAndSettle();

      // Verifies the widget tree reaches a settled state without throwing
      // infinite rebuild / timeout exception
      expect(find.text('Người thân'), findsOneWidget);

      // Verify loadSavedRelatives was triggered on event, not an infinite loop
      verify(() => mockRelationshipRepo.getRelatives()).called(greaterThanOrEqualTo(1));
    });

    testWidgets('TC-FE-16: BookAppointmentScreen_EmptyRelatives_AddButton displays empty state and direct button',
        (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      when(() => mockRelationshipRepo.getRelatives()).thenAnswer((_) async => []);

      await tester.pumpWidget(buildTestWidget());
      await tester.pumpAndSettle();

      // Switch to "Người thân"
      final relativeRadio = find.text('Người thân');
      await tester.ensureVisible(relativeRadio);
      await tester.pumpAndSettle();

      await tester.tap(relativeRadio);
      await tester.pumpAndSettle();

      // Expect empty state message
      expect(
        find.text('Chưa có người thân nào. Hãy thêm người thân để đặt lịch hộ.'),
        findsOneWidget,
      );

      // Expect direct navigation button '+ THÊM NGƯỜI THÂN NGAY'
      final addRelativeButton = find.text('+ THÊM NGƯỜI THÂN NGAY');
      expect(addRelativeButton, findsOneWidget);
      expect(find.byType(ElevatedButton), findsWidgets);
    });
  });
}
