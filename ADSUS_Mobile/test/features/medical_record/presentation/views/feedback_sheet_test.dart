import 'package:adsus_mobile/features/medical_record/presentation/views/feedback_sheet.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  Widget buildSheet({required void Function(int, String?) onSubmit}) {
    return MaterialApp(
      home: Scaffold(
        body: FeedbackSheet(onSubmit: onSubmit),
      ),
    );
  }

  group('FeedbackSheet', () {
    testWidgets('submit button disabled when no star selected', (tester) async {
      await tester.pumpWidget(buildSheet(onSubmit: (_, __) {}));
      await tester.pumpAndSettle();

      final btn = tester.widget<ElevatedButton>(
        find.widgetWithText(ElevatedButton, 'Gửi phản hồi'),
      );
      expect(btn.onPressed, isNull);
    });

    testWidgets('submit button enabled after tapping star 3', (tester) async {
      await tester.pumpWidget(buildSheet(onSubmit: (_, __) {}));
      await tester.pumpAndSettle();

      // 5 unselected stars initially
      final starIcons = find.byIcon(Icons.star_border);
      expect(starIcons, findsNWidgets(5));

      // Tap 3rd star
      await tester.tap(starIcons.at(2));
      await tester.pump();

      final btn = tester.widget<ElevatedButton>(
        find.widgetWithText(ElevatedButton, 'Gửi phản hồi'),
      );
      expect(btn.onPressed, isNotNull);
    });

    testWidgets('tapping star changes selected count', (tester) async {
      await tester.pumpWidget(buildSheet(onSubmit: (_, __) {}));
      await tester.pumpAndSettle();

      // Tap 4th star
      await tester.tap(find.byIcon(Icons.star_border).at(3));
      await tester.pump();

      // 4 filled stars should appear
      expect(find.byIcon(Icons.star), findsNWidgets(4));
      expect(find.byIcon(Icons.star_border), findsNWidgets(1));
    });

    testWidgets('submit with only rating calls onSubmit(rating, empty string)', (tester) async {
      int? capturedRating;
      String? capturedContent;

      await tester.pumpWidget(buildSheet(onSubmit: (r, c) {
        capturedRating = r;
        capturedContent = c;
      }));
      await tester.pumpAndSettle();

      // Tap 5th star
      await tester.tap(find.byIcon(Icons.star_border).at(4));
      await tester.pump();

      await tester.tap(find.widgetWithText(ElevatedButton, 'Gửi phản hồi'));
      await tester.pumpAndSettle();

      expect(capturedRating, 5);
      expect(capturedContent, '');
    });

    testWidgets('submit with content calls onSubmit with content string', (tester) async {
      int? capturedRating;
      String? capturedContent;

      await tester.pumpWidget(buildSheet(onSubmit: (r, c) {
        capturedRating = r;
        capturedContent = c;
      }));
      await tester.pumpAndSettle();

      // Tap 3rd star
      await tester.tap(find.byIcon(Icons.star_border).at(2));
      await tester.pump();

      // Enter content
      await tester.enterText(find.byType(TextField), 'Bác sĩ rất tận tâm');
      await tester.pump();

      await tester.tap(find.widgetWithText(ElevatedButton, 'Gửi phản hồi'));
      await tester.pumpAndSettle();

      expect(capturedRating, 3);
      expect(capturedContent, 'Bác sĩ rất tận tâm');
    });
  });
}
