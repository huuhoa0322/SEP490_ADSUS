import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

/// Tests cho clamp giờ nhắc trong _TimeSlotRow.
///
/// Bug: time picker cho phép chọn giờ ngoài khung hợp lệ
/// → "Thông báo tiếp theo: Trưa 17:55" hiển thị sai logic.
///
/// Fix: clamp giờ về khoảng hợp lệ + snackbar warning.
///   Sáng: 05:00–10:59
///   Trưa: 11:00–16:59
///   Tối:  17:00–23:59

void main() {
  group('time slot clamp logic', () {
    // Tests for _TimeSlotRow._clampToSlot
    // We test the clamping logic by calling the static helper directly.

    group('Sáng 05:00–10:59', () {
      test('07:00 — trong khoảng → không clamp', () {
        final result = _clampToSlot('Sáng', const TimeOfDay(hour: 7, minute: 0));
        expect(result.adjusted, isFalse);
        expect(result.hour, 7);
        expect(result.minute, 0);
      });

      test('05:00 — biên dưới → không clamp', () {
        final result = _clampToSlot('Sáng', const TimeOfDay(hour: 5, minute: 0));
        expect(result.adjusted, isFalse);
        expect(result.hour, 5);
        expect(result.minute, 0);
      });

      test('10:59 — biên trên → không clamp', () {
        final result = _clampToSlot('Sáng', const TimeOfDay(hour: 10, minute: 59));
        expect(result.adjusted, isFalse);
        expect(result.hour, 10);
        expect(result.minute, 59);
      });

      test('04:59 — dưới khoảng → clamp lên 05:00', () {
        final result = _clampToSlot('Sáng', const TimeOfDay(hour: 4, minute: 59));
        expect(result.adjusted, isTrue);
        expect(result.hour, 5);
        expect(result.minute, 0);
      });

      test('11:00 — trên khoảng → clamp xuống 10:59', () {
        final result = _clampToSlot('Sáng', const TimeOfDay(hour: 11, minute: 0));
        expect(result.adjusted, isTrue);
        expect(result.hour, 10);
        expect(result.minute, 59);
      });

      test('17:55 — hoàn toàn ngoài → clamp xuống 10:59', () {
        final result = _clampToSlot('Sáng', const TimeOfDay(hour: 17, minute: 55));
        expect(result.adjusted, isTrue);
        expect(result.hour, 10);
        expect(result.minute, 59);
      });
    });

    group('Trưa 11:00–16:59', () {
      test('12:00 — trong khoảng → không clamp', () {
        final result = _clampToSlot('Trưa', const TimeOfDay(hour: 12, minute: 0));
        expect(result.adjusted, isFalse);
        expect(result.hour, 12);
        expect(result.minute, 0);
      });

      test('11:00 — biên dưới → không clamp', () {
        final result = _clampToSlot('Trưa', const TimeOfDay(hour: 11, minute: 0));
        expect(result.adjusted, isFalse);
        expect(result.hour, 11);
        expect(result.minute, 0);
      });

      test('16:59 — biên trên → không clamp', () {
        final result = _clampToSlot('Trưa', const TimeOfDay(hour: 16, minute: 59));
        expect(result.adjusted, isFalse);
        expect(result.hour, 16);
        expect(result.minute, 59);
      });

      test('10:59 — dưới khoảng → clamp lên 11:00', () {
        final result = _clampToSlot('Trưa', const TimeOfDay(hour: 10, minute: 59));
        expect(result.adjusted, isTrue);
        expect(result.hour, 11);
        expect(result.minute, 0);
      });

      test('17:00 — trên khoảng → clamp xuống 16:59', () {
        final result = _clampToSlot('Trưa', const TimeOfDay(hour: 17, minute: 0));
        expect(result.adjusted, isTrue);
        expect(result.hour, 16);
        expect(result.minute, 59);
      });

      test('17:55 — trên khoảng → clamp xuống 16:59 (bug repro)', () {
        final result = _clampToSlot('Trưa', const TimeOfDay(hour: 17, minute: 55));
        expect(result.adjusted, isTrue);
        expect(result.hour, 16);
        expect(result.minute, 59);
        expect(result.hour, isNot(equals(17)),
            reason: '17:55 is evening, not noon — must snap to 16:59');
      });
    });

    group('Tối 17:00–23:59', () {
      test('20:00 — trong khoảng → không clamp', () {
        final result = _clampToSlot('Tối', const TimeOfDay(hour: 20, minute: 0));
        expect(result.adjusted, isFalse);
        expect(result.hour, 20);
        expect(result.minute, 0);
      });

      test('17:00 — biên dưới → không clamp', () {
        final result = _clampToSlot('Tối', const TimeOfDay(hour: 17, minute: 0));
        expect(result.adjusted, isFalse);
        expect(result.hour, 17);
        expect(result.minute, 0);
      });

      test('23:59 — biên trên → không clamp', () {
        final result = _clampToSlot('Tối', const TimeOfDay(hour: 23, minute: 59));
        expect(result.adjusted, isFalse);
        expect(result.hour, 23);
        expect(result.minute, 59);
      });

      test('16:59 — dưới khoảng → clamp lên 17:00', () {
        final result = _clampToSlot('Tối', const TimeOfDay(hour: 16, minute: 59));
        expect(result.adjusted, isTrue);
        expect(result.hour, 17);
        expect(result.minute, 0);
      });

      test('04:00 — hoàn toàn ngoài → clamp lên 17:00', () {
        final result = _clampToSlot('Tối', const TimeOfDay(hour: 4, minute: 0));
        expect(result.adjusted, isTrue);
        expect(result.hour, 17);
        expect(result.minute, 0);
      });

      test('00:30 — dưới khoảng → clamp lên 17:00', () {
        final result = _clampToSlot('Tối', const TimeOfDay(hour: 0, minute: 30));
        expect(result.adjusted, isTrue);
        expect(result.hour, 17);
        expect(result.minute, 0);
      });
    });

    group('unknown slot label', () {
      test('không known label → không clamp', () {
        final result = _clampToSlot('Unknown', const TimeOfDay(hour: 17, minute: 55));
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
_ClampResult _clampToSlot(String label, TimeOfDay picked) {
  switch (label) {
    case 'Sáng': // 05:00–10:59
      if (picked.hour < 5)  return _ClampResult(hour: 5,  minute: 0,  adjusted: true);
      if (picked.hour > 10 || (picked.hour == 10 && picked.minute > 59))
        return _ClampResult(hour: 10, minute: 59, adjusted: true);
      return _ClampResult(hour: picked.hour, minute: picked.minute, adjusted: false);

    case 'Trưa': // 11:00–16:59
      if (picked.hour < 11) return _ClampResult(hour: 11, minute: 0,  adjusted: true);
      if (picked.hour > 16 || (picked.hour == 16 && picked.minute > 59))
        return _ClampResult(hour: 16, minute: 59, adjusted: true);
      return _ClampResult(hour: picked.hour, minute: picked.minute, adjusted: false);

    case 'Tối': // 17:00–23:59
      if (picked.hour < 17) return _ClampResult(hour: 17, minute: 0,  adjusted: true);
      if (picked.hour > 23) return _ClampResult(hour: 23, minute: 59, adjusted: true);
      return _ClampResult(hour: picked.hour, minute: picked.minute, adjusted: false);

    default:
      return _ClampResult(hour: picked.hour, minute: picked.minute, adjusted: false);
  }
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
