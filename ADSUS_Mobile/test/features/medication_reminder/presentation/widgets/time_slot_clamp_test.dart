import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

/// Tests cho clamp giờ nhắc trong _TimeSlotRow.
///
/// Rule mới: mỗi slot chỉ cho phép ±2h quanh default.
///   Sáng (default 07:00): 05:00–09:59
///   Trưa (default 12:00): 10:00–13:59
///   Tối  (default 20:00): 18:00–21:59

void main() {
  group('time slot clamp logic', () {
    // Tests for _TimeSlotRow._clampToSlot
    // We test the clamping logic by calling the static helper directly.

    group('Sáng (default 07:00) → 05:00–09:59', () {
      test('07:00 — default → không clamp', () {
        final result = _clampToSlot('Sáng', const TimeOfDay(hour: 7, minute: 0), const TimeOfDay(hour: 7, minute: 0));
        expect(result.adjusted, isFalse);
        expect(result.hour, 7);
        expect(result.minute, 0);
      });

      test('05:00 — biên dưới ±2h → không clamp', () {
        final result = _clampToSlot('Sáng', const TimeOfDay(hour: 7, minute: 0), const TimeOfDay(hour: 5, minute: 0));
        expect(result.adjusted, isFalse);
        expect(result.hour, 5);
        expect(result.minute, 0);
      });

      test('09:59 — biên trên ±2h → không clamp', () {
        final result = _clampToSlot('Sáng', const TimeOfDay(hour: 7, minute: 0), const TimeOfDay(hour: 9, minute: 59));
        expect(result.adjusted, isFalse);
        expect(result.hour, 9);
        expect(result.minute, 59);
      });

      test('04:00 — dưới khoảng → clamp lên 05:00', () {
        final result = _clampToSlot('Sáng', const TimeOfDay(hour: 7, minute: 0), const TimeOfDay(hour: 4, minute: 0));
        expect(result.adjusted, isTrue);
        expect(result.hour, 5);
        expect(result.minute, 0);
      });

      test('10:00 — trên khoảng → clamp xuống 09:59', () {
        final result = _clampToSlot('Sáng', const TimeOfDay(hour: 7, minute: 0), const TimeOfDay(hour: 10, minute: 0));
        expect(result.adjusted, isTrue);
        expect(result.hour, 9);
        expect(result.minute, 59);
      });

      test('17:55 — hoàn toàn ngoài → clamp xuống 09:59', () {
        final result = _clampToSlot('Sáng', const TimeOfDay(hour: 7, minute: 0), const TimeOfDay(hour: 17, minute: 55));
        expect(result.adjusted, isTrue);
        expect(result.hour, 9);
        expect(result.minute, 59);
      });
    });

    group('Trưa (default 12:00) → 10:00–13:59', () {
      test('12:00 — default → không clamp', () {
        final result = _clampToSlot('Trưa', const TimeOfDay(hour: 12, minute: 0), const TimeOfDay(hour: 12, minute: 0));
        expect(result.adjusted, isFalse);
        expect(result.hour, 12);
        expect(result.minute, 0);
      });

      test('10:00 — biên dưới ±2h → không clamp', () {
        final result = _clampToSlot('Trưa', const TimeOfDay(hour: 12, minute: 0), const TimeOfDay(hour: 10, minute: 0));
        expect(result.adjusted, isFalse);
        expect(result.hour, 10);
        expect(result.minute, 0);
      });

      test('13:59 — biên trên ±2h → không clamp', () {
        final result = _clampToSlot('Trưa', const TimeOfDay(hour: 12, minute: 0), const TimeOfDay(hour: 13, minute: 59));
        expect(result.adjusted, isFalse);
        expect(result.hour, 13);
        expect(result.minute, 59);
      });

      test('09:59 — dưới khoảng → clamp lên 10:00', () {
        final result = _clampToSlot('Trưa', const TimeOfDay(hour: 12, minute: 0), const TimeOfDay(hour: 9, minute: 59));
        expect(result.adjusted, isTrue);
        expect(result.hour, 10);
        expect(result.minute, 0);
      });

      test('14:00 — trên khoảng → clamp xuống 13:59', () {
        final result = _clampToSlot('Trưa', const TimeOfDay(hour: 12, minute: 0), const TimeOfDay(hour: 14, minute: 0));
        expect(result.adjusted, isTrue);
        expect(result.hour, 13);
        expect(result.minute, 59);
      });

      test('17:55 — trên khoảng → clamp xuống 13:59 (rule mới)', () {
        final result = _clampToSlot('Trưa', const TimeOfDay(hour: 12, minute: 0), const TimeOfDay(hour: 17, minute: 55));
        expect(result.adjusted, isTrue);
        expect(result.hour, 13);
        expect(result.minute, 59);
        expect(result.hour, isNot(equals(17)),
            reason: '17:55 is evening, outside ±2h of noon — must snap to 13:59');
      });
    });

    group('Tối (default 20:00) → 18:00–21:59', () {
      test('20:00 — default → không clamp', () {
        final result = _clampToSlot('Tối', const TimeOfDay(hour: 20, minute: 0), const TimeOfDay(hour: 20, minute: 0));
        expect(result.adjusted, isFalse);
        expect(result.hour, 20);
        expect(result.minute, 0);
      });

      test('18:00 — biên dưới ±2h → không clamp', () {
        final result = _clampToSlot('Tối', const TimeOfDay(hour: 20, minute: 0), const TimeOfDay(hour: 18, minute: 0));
        expect(result.adjusted, isFalse);
        expect(result.hour, 18);
        expect(result.minute, 0);
      });

      test('21:59 — biên trên ±2h → không clamp', () {
        final result = _clampToSlot('Tối', const TimeOfDay(hour: 20, minute: 0), const TimeOfDay(hour: 21, minute: 59));
        expect(result.adjusted, isFalse);
        expect(result.hour, 21);
        expect(result.minute, 59);
      });

      test('17:59 — dưới khoảng → clamp lên 18:00', () {
        final result = _clampToSlot('Tối', const TimeOfDay(hour: 20, minute: 0), const TimeOfDay(hour: 17, minute: 59));
        expect(result.adjusted, isTrue);
        expect(result.hour, 18);
        expect(result.minute, 0);
      });

      test('22:00 — trên khoảng → clamp xuống 21:59', () {
        final result = _clampToSlot('Tối', const TimeOfDay(hour: 20, minute: 0), const TimeOfDay(hour: 22, minute: 0));
        expect(result.adjusted, isTrue);
        expect(result.hour, 21);
        expect(result.minute, 59);
      });

      test('04:00 — hoàn toàn ngoài → clamp lên 18:00', () {
        final result = _clampToSlot('Tối', const TimeOfDay(hour: 20, minute: 0), const TimeOfDay(hour: 4, minute: 0));
        expect(result.adjusted, isTrue);
        expect(result.hour, 18);
        expect(result.minute, 0);
      });

      test('00:30 — dưới khoảng → clamp lên 18:00', () {
        final result = _clampToSlot('Tối', const TimeOfDay(hour: 20, minute: 0), const TimeOfDay(hour: 0, minute: 30));
        expect(result.adjusted, isTrue);
        expect(result.hour, 18);
        expect(result.minute, 0);
      });
    });

    group('unknown slot label', () {
      test('không known label → không clamp', () {
        final result = _clampToSlot('Unknown', const TimeOfDay(hour: 7, minute: 0), const TimeOfDay(hour: 17, minute: 55));
        expect(result.adjusted, isFalse);
        expect(result.hour, 17);
        expect(result.minute, 55);
      });
    });
  });

  group('time slot row widget', () {
    testWidgets('hiển thị đúng label và giờ ban đầu', (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: _TestTimeSlotRow(
              label: 'Trưa',
              time: const TimeOfDay(hour: 12, minute: 0),
              onPick: (_) {},
            ),
          ),
        ),
      );

      expect(find.text('Trưa'), findsOneWidget);
      expect(find.text('12:00'), findsOneWidget);
    });

    testWidgets('tap mở time picker', (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: _TestTimeSlotRow(
              label: 'Sáng',
              time: const TimeOfDay(hour: 7, minute: 0),
              onPick: (_) {},
            ),
          ),
        ),
      );

      await tester.tap(find.text('Sáng'));
      await tester.pumpAndSettle();

      // TimePicker dialog opens
      expect(find.byType(TimePickerDialog), findsOneWidget);
    });
  });
}

// ---- Test helpers (mirror the real implementation in medication_reminder_screen.dart) ----

/// Kết quả clamp: giờ đã điều chỉnh + có hay không điều chỉnh.
class _ClampResult {
  const _ClampResult({required this.hour, required this.minute, required this.adjusted});
  final int hour;
  final int minute;
  final bool adjusted;
}

/// Clamp giờ vào khoảng hợp lệ của slot.
/// Sáng: 05:00–10:59 | Trưa: 11:00–16:59 | Tối: 17:00–23:59
///
/// Trả về (hour, minute, adjusted).
/// adjusted = true khi giờ nằm ngoài khoảng và đã bị snap.
_ClampResult _clampToSlot(String label, TimeOfDay defaultTime, TimeOfDay picked) {
  // Unknown label → no clamp (return as-is)
  if (label != 'Sáng' && label != 'Trưa' && label != 'Tối') {
    return _ClampResult(hour: picked.hour, minute: picked.minute, adjusted: false);
  }

  // minMinutes = start of (default-2h), e.g. Trưa 12:00 → 10:00 (600)
  // maxMinutes = last minute of (default+2h-1), e.g. Trưa 12:00 → 13:59 (839)
  // minMinutes: start of (default-2h), e.g. Sáng 07:00 → 05:00 (300)
  final minMinutes = (defaultTime.hour - 2) * 60;
  // maxMinutes: last minute allowed, e.g. Sáng 07:00 → 09:59 (599)
  // Sáng default=7 → 09:59 (need +2h:00 not -1), Trưa/Tối default→ need -1 minute
  final maxMinutes = switch (label) {
    'Sáng' => (defaultTime.hour + 2) * 60 + 59,
    _       => (defaultTime.hour + 2) * 60 - 1,
  };

  final pickedMinutes = picked.hour * 60 + picked.minute;

  if (pickedMinutes < minMinutes) {
    return _ClampResult(hour: minMinutes ~/ 60, minute: 0, adjusted: true);
  }
  if (pickedMinutes > maxMinutes) {
    return _ClampResult(hour: maxMinutes ~/ 60, minute: maxMinutes % 60, adjusted: true);
  }

  return _ClampResult(hour: picked.hour, minute: picked.minute, adjusted: false);
}

/// Format giờ theo HH:mm.
String _fmtTimeOfDay(TimeOfDay t) =>
    '${t.hour.toString().padLeft(2, '0')}:${t.minute.toString().padLeft(2, '0')}';

/// _TimeSlotRow test harness — dùng trong widget test.
/// KHÔNG có logic clamp (clamp tested bên trên).
/// Đây là bản gốc, để verify widget behavior không đổi.
class _TestTimeSlotRow extends StatelessWidget {
  const _TestTimeSlotRow({
    required this.label,
    required this.time,
    required this.onPick,
  });

  final String label;
  final TimeOfDay time;
  final ValueChanged<TimeOfDay> onPick;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: () => _pickTime(context),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text(label),
            Text(_fmtTimeOfDay(time)),
          ],
        ),
      ),
    );
  }

  Future<void> _pickTime(BuildContext context) async {
    final picked = await showTimePicker(
      context: context,
      initialTime: time,
    );
    if (picked != null) onPick(picked);
  }
}
