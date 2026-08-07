import 'package:adsus_mobile/core/theme/app_theme.dart';
import 'package:adsus_mobile/features/medication_reminder/presentation/widgets/adherence_pill_badge.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

/// AdherencePillBadge — hiển thị tỉ lệ tuân thủ thuốc của bệnh nhân.
///
/// Quy tắc nghiệp vụ (CLAUDE.md §11.3.4):
///   - adherence ≥ 80%  → tuân thủ tốt  (variant "good")
///   - adherence < 80%  → cần hỗ trợ    (variant "warn")
///   - KHÔNG dùng màu đỏ / destructive cho adherence thấp — chỉ amber.
///     Màu đỏ dành cho safety card, validation error, "không được".
///
/// Giá trị không xác định (null/NaN) → variant "unknown" (màu muted).

void main() {
  group('AdherencePillBadge', () {
    testWidgets('hiển thị phần trăm khi adherence = 85%', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: Scaffold(body: AdherencePillBadge(percent: 85))),
      );

      expect(find.text('85%'), findsOneWidget);
    });

    testWidgets('làm tròn phần trăm đến số nguyên', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: Scaffold(body: AdherencePillBadge(percent: 79.6))),
      );

      expect(find.text('80%'), findsOneWidget);
    });

    testWidgets('hiển thị variant good khi adherence ≥ 80%', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: Scaffold(body: AdherencePillBadge(percent: 80))),
      );

      final pill = tester.widget<Container>(
        find.byKey(const Key('adherence-pill')),
      );

      // Variant good: bg teal-tint, text teal
      final decoration = pill.decoration as BoxDecoration;
      expect(decoration.color, equals(AppColors.teal.withValues(alpha: 0.1)));
      expect(decoration.border?.top.color, equals(AppColors.success.withValues(alpha: 0.3)));
    });

    testWidgets('hiển thị variant warn khi adherence < 80%', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: Scaffold(body: AdherencePillBadge(percent: 79))),
      );

      final pill = tester.widget<Container>(
        find.byKey(const Key('adherence-pill')),
      );

      final decoration = pill.decoration as BoxDecoration;
      expect(decoration.color, equals(AppColors.amberWarn.withValues(alpha: 0.1)));
      expect(decoration.border?.top.color, equals(AppColors.amberWarn.withValues(alpha: 0.3)));
    });

    testWidgets('KHÔNG dùng variant destructive khi adherence = 0%', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: Scaffold(body: AdherencePillBadge(percent: 0))),
      );

      // 0% → warn, không phải destructive (đỏ)
      final pill = tester.widget<Container>(
        find.byKey(const Key('adherence-pill')),
      );

      final decoration = pill.decoration as BoxDecoration;
      // Must be amber, NOT danger/red
      expect(decoration.color, isNot(AppColors.danger));
      expect(decoration.color, equals(AppColors.amberWarn.withValues(alpha: 0.1)));
    });

    testWidgets('hiển thị variant unknown khi percent = null', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: Scaffold(body: AdherencePillBadge(percent: null))),
      );

      expect(find.text('—'), findsOneWidget);

      final pill = tester.widget<Container>(
        find.byKey(const Key('adherence-pill')),
      );
      final decoration = pill.decoration as BoxDecoration;
      expect(decoration.color, equals(AppColors.muted.withValues(alpha: 0.1)));
    });

    testWidgets('chấp nhận label tuỳ biến (vd: tuần này)', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(body: AdherencePillBadge(percent: 90, label: 'tuần này')),
        ),
      );

      expect(find.text('tuần này'), findsOneWidget);
    });
  });
}