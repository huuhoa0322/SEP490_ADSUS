import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:adsus_mobile/features/patient_relationship/domain/entities/patient_relationship.dart';
import 'package:adsus_mobile/features/patient_relationship/presentation/views/widgets/relative_card.dart';

void main() {
  final sampleRelative = PatientRelationship(
    relationshipId: 'rel-01',
    patientProfileId: 'patient-01',
    fullName: 'Trần Thị Hoa',
    relationshipName: 'Mẹ',
    phone: '0987654321',
    dateOfBirth: DateTime(1970, 5, 15),
    createdAt: DateTime(2026, 1, 10),
  );

  Widget buildTestWidget({
    required PatientRelationship relative,
    bool isDeleting = false,
    VoidCallback? onDelete,
    VoidCallback? onEdit,
  }) {
    return MaterialApp(
      home: Scaffold(
        body: Center(
          child: RelativeCard(
            relative: relative,
            isDeleting: isDeleting,
            onDelete: onDelete ?? () {},
            onEdit: onEdit,
          ),
        ),
      ),
    );
  }

  group('RelativeCard Widget Tests', () {
    testWidgets('TC-REL-01: renders basic relative information on card',
        (tester) async {
      await tester.pumpWidget(buildTestWidget(relative: sampleRelative));
      await tester.pumpAndSettle();

      expect(find.text('Trần Thị Hoa'), findsOneWidget);
      expect(find.text('Mẹ'), findsOneWidget);
      expect(find.text('0987654321'), findsOneWidget);
      expect(find.text('${sampleRelative.age} tuổi'), findsOneWidget);
      expect(find.text('T'), findsOneWidget); // Avatar initial
    });

    testWidgets('TC-REL-02: tapping RelativeCard opens _RelativeDetailSheet',
        (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(buildTestWidget(relative: sampleRelative));
      await tester.pumpAndSettle();

      // Tap on the card
      await tester.tap(find.byType(RelativeCard));
      await tester.pumpAndSettle();

      // BottomSheet should appear
      expect(find.byType(BottomSheet), findsOneWidget);
      expect(find.byType(RelativeDetailSheet), findsOneWidget);
      expect(find.text('Chi tiết người thân'), findsOneWidget);

      // Verify fields inside sheet
      expect(find.text('0987 654 321'), findsOneWidget); // Formatted phone
      expect(find.text('15/05/1970'), findsOneWidget); // Formatted DoB
      expect(find.text('10/01/2026'), findsOneWidget); // Formatted createdAt
      expect(find.text('ĐÓNG'), findsOneWidget);
    });

    testWidgets('TC-REL-03: closes BottomSheet when tapping close icon or ĐÓNG button',
        (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(buildTestWidget(relative: sampleRelative));
      await tester.pumpAndSettle();

      // Tap card to open
      await tester.tap(find.byType(RelativeCard));
      await tester.pumpAndSettle();
      expect(find.byType(RelativeDetailSheet), findsOneWidget);

      // Tap 'ĐÓNG' button
      await tester.tap(find.text('ĐÓNG'));
      await tester.pumpAndSettle();
      expect(find.byType(RelativeDetailSheet), findsNothing);

      // Open again and tap close icon button
      await tester.tap(find.byType(RelativeCard));
      await tester.pumpAndSettle();
      expect(find.byType(RelativeDetailSheet), findsOneWidget);

      await tester.tap(find.byTooltip('Đóng'));
      await tester.pumpAndSettle();
      expect(find.byType(RelativeDetailSheet), findsNothing);
    });

    testWidgets('TC-REL-04: strips HTML and XSS script tags and payloads cleanly',
        (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      // Malicious XSS payloads
      final maliciousRelative = PatientRelationship(
        relationshipId: 'rel-xss',
        patientProfileId: 'patient-01',
        fullName: '<b>Nguyễn</b><script>alert(1)</script>',
        relationshipName: '<i>Bố</i>',
        phone: '0912345678<script>alert(2)</script>',
        dateOfBirth: DateTime(1980, 2, 20),
        createdAt: DateTime(2026, 3, 1),
      );

      await tester.pumpWidget(buildTestWidget(relative: maliciousRelative));
      await tester.pumpAndSettle();

      // On card
      expect(find.text('Nguyễn'), findsOneWidget);
      expect(find.text('Bố'), findsOneWidget);
      expect(find.textContaining('<script>'), findsNothing);
      expect(find.textContaining('<b>'), findsNothing);
      expect(find.textContaining('<i>'), findsNothing);
      expect(find.textContaining('alert(1)'), findsNothing);
      expect(find.textContaining('alert(2)'), findsNothing);

      // Tap to open BottomSheet
      await tester.tap(find.byType(RelativeCard));
      await tester.pumpAndSettle();

      // Inside BottomSheet
      expect(find.byType(RelativeDetailSheet), findsOneWidget);
      expect(find.text('0912 345 678'), findsOneWidget);
      expect(find.textContaining('<script>'), findsNothing);
      expect(find.textContaining('<b>'), findsNothing);
      expect(find.textContaining('<i>'), findsNothing);
      expect(find.textContaining('alert(1)'), findsNothing);
      expect(find.textContaining('alert(2)'), findsNothing);
    });

    testWidgets('TC-REL-05: handles null and empty fields with clean fallbacks',
        (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      final minimalRelative = PatientRelationship(
        relationshipId: 'rel-min',
        patientProfileId: 'patient-01',
        fullName: '',
        relationshipName: null,
        phone: null,
        dateOfBirth: null,
        createdAt: DateTime(2026, 4, 1),
      );

      await tester.pumpWidget(buildTestWidget(relative: minimalRelative));
      await tester.pumpAndSettle();

      // Card should display fallback name
      expect(find.text('Chưa có thông tin'), findsOneWidget);

      // Tap card
      await tester.tap(find.byType(RelativeCard));
      await tester.pumpAndSettle();

      expect(find.byType(RelativeDetailSheet), findsOneWidget);
      expect(find.text('Chưa đặt nhãn'), findsOneWidget);
      expect(find.text('Chưa cập nhật'), findsWidgets); // Phone, DoB, Tuổi
      expect(find.text('01/04/2026'), findsOneWidget);
    });

    testWidgets('TC-REL-06: triggers onDelete callback when delete button tapped',
        (tester) async {
      var deleteCalled = false;
      await tester.pumpWidget(buildTestWidget(
        relative: sampleRelative,
        onDelete: () => deleteCalled = true,
      ));
      await tester.pumpAndSettle();

      await tester.tap(find.byTooltip('Xóa người thân'));
      await tester.pumpAndSettle();

      expect(deleteCalled, isTrue);
    });

    testWidgets('TC-REL-07: triggers onEdit callback when edit button tapped',
        (tester) async {
      var editCalled = false;
      await tester.pumpWidget(buildTestWidget(
        relative: sampleRelative,
        onEdit: () => editCalled = true,
      ));
      await tester.pumpAndSettle();

      expect(find.byTooltip('Chỉnh sửa thông tin'), findsOneWidget);
      await tester.tap(find.byTooltip('Chỉnh sửa thông tin'));
      await tester.pumpAndSettle();

      expect(editCalled, isTrue);
    });

    testWidgets('TC-REL-08: displays edit button inside RelativeDetailSheet',
        (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(buildTestWidget(relative: sampleRelative));
      await tester.pumpAndSettle();

      await tester.tap(find.byType(RelativeCard));
      await tester.pumpAndSettle();

      expect(find.byType(RelativeDetailSheet), findsOneWidget);
      expect(find.widgetWithText(ElevatedButton, 'Chỉnh sửa thông tin'), findsOneWidget);
      expect(find.text('ĐÓNG'), findsOneWidget);
    });
  });
}
