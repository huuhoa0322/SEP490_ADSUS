import 'package:adsus_mobile/features/medical_record/domain/entities/medical_record_feedback.dart';
import 'package:adsus_mobile/features/medical_record/presentation/widgets/feedback_card.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  Widget buildCard(MedicalRecordFeedback feedback) {
    return MaterialApp(
      home: Scaffold(body: FeedbackCard(feedback: feedback)),
    );
  }

  group('FeedbackCard', () {
    testWidgets('hien dung so sao da chon va noi dung feedback', (tester) async {
      final feedback = MedicalRecordFeedback(
        id: 'feedback-1',
        rating: 3,
        content: 'Bac si rat tan tam',
        submittedAt: DateTime(2026, 8, 20),
      );

      await tester.pumpWidget(buildCard(feedback));

      expect(find.byIcon(Icons.star), findsNWidgets(3));
      expect(find.byIcon(Icons.star_border), findsNWidgets(2));
      expect(find.text('Bac si rat tan tam'), findsOneWidget);
      expect(find.text('20/08/2026'), findsOneWidget);
    });

    testWidgets('content null hoac rong thi khong render dong noi dung', (tester) async {
      final feedback = MedicalRecordFeedback(
        id: 'feedback-1',
        rating: 5,
        submittedAt: DateTime(2026, 8, 20),
      );

      await tester.pumpWidget(buildCard(feedback));

      expect(find.byIcon(Icons.star), findsNWidgets(5));
      expect(find.byIcon(Icons.star_border), findsNothing);
    });
  });
}
