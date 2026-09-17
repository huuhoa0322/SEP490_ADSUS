import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:adsus_mobile/features/patient_relationship/domain/entities/patient_relationship.dart';
import 'package:adsus_mobile/features/patient_relationship/domain/repositories/patient_relationship_repository.dart';
import 'package:adsus_mobile/features/patient_relationship/presentation/views/edit_relative_screen.dart';
import 'package:adsus_mobile/shared/providers/app_providers.dart';

class _MockPatientRelationshipRepo extends Mock
    implements PatientRelationshipRepository {}

void main() {
  late _MockPatientRelationshipRepo mockRepo;

  final sampleRelative = PatientRelationship(
    relationshipId: 'rel-test-01',
    patientProfileId: 'patient-test-01',
    fullName: 'Trần Thị Hoa',
    phone: '0987654321',
    relationshipName: 'Mẹ',
    dateOfBirth: DateTime(1975, 8, 20),
    createdAt: DateTime(2026, 1, 1),
  );

  setUp(() {
    mockRepo = _MockPatientRelationshipRepo();
    when(() => mockRepo.getRelatives()).thenAnswer((_) async => [sampleRelative]);
  });

  Widget buildTestWidget({required PatientRelationship relative}) {
    return ProviderScope(
      overrides: [
        patientRelationshipRepositoryProvider.overrideWithValue(mockRepo),
      ],
      child: MaterialApp(
        home: EditRelativeScreen(relative: relative),
      ),
    );
  }

  group('EditRelativeScreen Widget Tests', () {
    testWidgets('TC-EDIT-01: pre-fills existing relative information',
        (tester) async {
      await tester.pumpWidget(buildTestWidget(relative: sampleRelative));
      await tester.pumpAndSettle();

      expect(find.text('Trần Thị Hoa'), findsOneWidget);
      expect(find.text('0987654321'), findsOneWidget);
      expect(find.text('Mẹ'), findsOneWidget);
      expect(find.text('20/08/1975'), findsOneWidget);
      expect(find.text('CẬP NHẬT THÔNG TIN'), findsOneWidget);
    });

    testWidgets('TC-EDIT-02: rejects HTML tags in full name field',
        (tester) async {
      await tester.pumpWidget(buildTestWidget(relative: sampleRelative));
      await tester.pumpAndSettle();

      final nameField = find.widgetWithText(TextFormField, 'Trần Thị Hoa');
      await tester.enterText(nameField, '<b>Trần Thị Hoa</b>');
      await tester.pumpAndSettle();

      await tester.tap(find.text('CẬP NHẬT THÔNG TIN'));
      await tester.pumpAndSettle();

      expect(find.text('Họ tên không được chứa thẻ HTML'), findsOneWidget);
      verifyNever(() => mockRepo.updateRelative(any(),
          fullName: any(named: 'fullName'),
          phone: any(named: 'phone'),
          dateOfBirth: any(named: 'dateOfBirth'),
          relationshipName: any(named: 'relationshipName')));
    });

    testWidgets('TC-EDIT-03: rejects HTML tags in relationship label field',
        (tester) async {
      await tester.pumpWidget(buildTestWidget(relative: sampleRelative));
      await tester.pumpAndSettle();

      final relField = find.widgetWithText(TextFormField, 'Mẹ');
      await tester.enterText(relField, '<script>alert(1)</script>');
      await tester.pumpAndSettle();

      await tester.tap(find.text('CẬP NHẬT THÔNG TIN'));
      await tester.pumpAndSettle();

      expect(find.text('Nhãn quan hệ không được chứa thẻ HTML'), findsOneWidget);
      verifyNever(() => mockRepo.updateRelative(any(),
          fullName: any(named: 'fullName'),
          phone: any(named: 'phone'),
          dateOfBirth: any(named: 'dateOfBirth'),
          relationshipName: any(named: 'relationshipName')));
    });

    testWidgets('TC-EDIT-04: rejects invalid phone number',
        (tester) async {
      await tester.pumpWidget(buildTestWidget(relative: sampleRelative));
      await tester.pumpAndSettle();

      final phoneField = find.widgetWithText(TextFormField, '0987654321');
      await tester.enterText(phoneField, '12345');
      await tester.pumpAndSettle();

      await tester.tap(find.text('CẬP NHẬT THÔNG TIN'));
      await tester.pumpAndSettle();

      expect(find.text('Số điện thoại không hợp lệ'), findsOneWidget);
      verifyNever(() => mockRepo.updateRelative(any(),
          fullName: any(named: 'fullName'),
          phone: any(named: 'phone'),
          dateOfBirth: any(named: 'dateOfBirth'),
          relationshipName: any(named: 'relationshipName')));
    });

    testWidgets('TC-EDIT-05: submits valid changes and calls updateRelative',
        (tester) async {
      final updated = PatientRelationship(
        relationshipId: 'rel-test-01',
        patientProfileId: 'patient-test-01',
        fullName: 'Trần Thị Hoa Cải',
        phone: '0987654322',
        relationshipName: 'Mẹ yêu',
        dateOfBirth: DateTime(1975, 8, 20),
        createdAt: DateTime(2026, 1, 1),
      );

      when(() => mockRepo.updateRelative(
            'rel-test-01',
            fullName: 'Trần Thị Hoa Cải',
            phone: '0987654322',
            dateOfBirth: any(named: 'dateOfBirth'),
            relationshipName: 'Mẹ yêu',
          )).thenAnswer((_) async => updated);

      await tester.pumpWidget(buildTestWidget(relative: sampleRelative));
      await tester.pumpAndSettle();

      final nameField = find.widgetWithText(TextFormField, 'Trần Thị Hoa');
      await tester.enterText(nameField, 'Trần Thị Hoa Cải');

      final phoneField = find.widgetWithText(TextFormField, '0987654321');
      await tester.enterText(phoneField, '0987654322');

      final relField = find.widgetWithText(TextFormField, 'Mẹ');
      await tester.enterText(relField, 'Mẹ yêu');

      await tester.pumpAndSettle();

      await tester.tap(find.text('CẬP NHẬT THÔNG TIN'));
      await tester.pumpAndSettle();

      verify(() => mockRepo.updateRelative(
            'rel-test-01',
            fullName: 'Trần Thị Hoa Cải',
            phone: '0987654322',
            dateOfBirth: any(named: 'dateOfBirth'),
            relationshipName: 'Mẹ yêu',
          )).called(1);
    });
  });
}
