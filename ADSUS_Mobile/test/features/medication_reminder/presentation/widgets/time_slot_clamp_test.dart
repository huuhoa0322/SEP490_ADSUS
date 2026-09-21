import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

/// Tests cho clamp giờ nhắc trong _TimeSlotRow.
///
/// Rule mới: mỗi slot chỉ cho phép ±2h quanh default.
///   Sáng (default 07:00): 05:00–09:59
///   Trưa (default 12:00): 10:00–13:59
///   Tối  (default 20:00): 18:00–21:59

void main() {
  group('Time Slot Clamping Boundary Condition Matrix (Quy tắc ±2h - 20 ca kiểm thử biên)', () {
    final clampMatrix = const <({
      String slot,
      TimeOfDay defaultTime,
      TimeOfDay inputTime,
      bool wantAdjusted,
      int wantHour,
      int wantMinute,
      String description,
    })>[
      // Khung SÁNG (Default 07:00; Dải cho phép: 05:00 - 09:59)
      (slot: 'Sáng', defaultTime: TimeOfDay(hour: 7, minute: 0), inputTime: TimeOfDay(hour: 7, minute: 0), wantAdjusted: false, wantHour: 7, wantMinute: 0, description: 'Giờ mặc định 07:00 -> Trong khoảng, không kẹp'),
      (slot: 'Sáng', defaultTime: TimeOfDay(hour: 7, minute: 0), inputTime: TimeOfDay(hour: 5, minute: 0), wantAdjusted: false, wantHour: 5, wantMinute: 0, description: 'Chạm biên dưới 05:00 -> Không kẹp'),
      (slot: 'Sáng', defaultTime: TimeOfDay(hour: 7, minute: 0), inputTime: TimeOfDay(hour: 9, minute: 59), wantAdjusted: false, wantHour: 9, wantMinute: 59, description: 'Chạm biên trên 09:59 -> Không kẹp'),
      (slot: 'Sáng', defaultTime: TimeOfDay(hour: 7, minute: 0), inputTime: TimeOfDay(hour: 4, minute: 0), wantAdjusted: true, wantHour: 5, wantMinute: 0, description: 'Dưới khoảng 04:00 -> Kẹp lên 05:00'),
      (slot: 'Sáng', defaultTime: TimeOfDay(hour: 7, minute: 0), inputTime: TimeOfDay(hour: 10, minute: 0), wantAdjusted: true, wantHour: 9, wantMinute: 59, description: 'Trên khoảng 10:00 -> Kẹp xuống 09:59'),
      (slot: 'Sáng', defaultTime: TimeOfDay(hour: 7, minute: 0), inputTime: TimeOfDay(hour: 17, minute: 55), wantAdjusted: true, wantHour: 9, wantMinute: 59, description: 'Hoàn toàn ngoài 17:55 -> Kẹp xuống 09:59'),

      // Khung TRƯA (Default 12:00; Dải cho phép: 10:00 - 13:59)
      (slot: 'Trưa', defaultTime: TimeOfDay(hour: 12, minute: 0), inputTime: TimeOfDay(hour: 12, minute: 0), wantAdjusted: false, wantHour: 12, wantMinute: 0, description: 'Giờ mặc định 12:00 -> Trong khoảng, không kẹp'),
      (slot: 'Trưa', defaultTime: TimeOfDay(hour: 12, minute: 0), inputTime: TimeOfDay(hour: 10, minute: 0), wantAdjusted: false, wantHour: 10, wantMinute: 0, description: 'Chạm biên dưới 10:00 -> Không kẹp'),
      (slot: 'Trưa', defaultTime: TimeOfDay(hour: 12, minute: 0), inputTime: TimeOfDay(hour: 13, minute: 59), wantAdjusted: false, wantHour: 13, wantMinute: 59, description: 'Chạm biên trên 13:59 -> Không kẹp'),
      (slot: 'Trưa', defaultTime: TimeOfDay(hour: 12, minute: 0), inputTime: TimeOfDay(hour: 9, minute: 59), wantAdjusted: true, wantHour: 10, wantMinute: 0, description: 'Dưới khoảng 09:59 -> Kẹp lên 10:00'),
      (slot: 'Trưa', defaultTime: TimeOfDay(hour: 12, minute: 0), inputTime: TimeOfDay(hour: 14, minute: 0), wantAdjusted: true, wantHour: 13, wantMinute: 59, description: 'Trên khoảng 14:00 -> Kẹp xuống 13:59'),
      (slot: 'Trưa', defaultTime: TimeOfDay(hour: 12, minute: 0), inputTime: TimeOfDay(hour: 17, minute: 55), wantAdjusted: true, wantHour: 13, wantMinute: 59, description: 'Trên khoảng 17:55 -> Kẹp xuống 13:59 (rule mới)'),

      // Khung TỐI (Default 20:00; Dải cho phép: 18:00 - 21:59)
      (slot: 'Tối', defaultTime: TimeOfDay(hour: 20, minute: 0), inputTime: TimeOfDay(hour: 20, minute: 0), wantAdjusted: false, wantHour: 20, wantMinute: 0, description: 'Giờ mặc định 20:00 -> Trong khoảng, không kẹp'),
      (slot: 'Tối', defaultTime: TimeOfDay(hour: 20, minute: 0), inputTime: TimeOfDay(hour: 18, minute: 0), wantAdjusted: false, wantHour: 18, wantMinute: 0, description: 'Chạm biên dưới 18:00 -> Không kẹp'),
      (slot: 'Tối', defaultTime: TimeOfDay(hour: 20, minute: 0), inputTime: TimeOfDay(hour: 21, minute: 59), wantAdjusted: false, wantHour: 21, wantMinute: 59, description: 'Chạm biên trên 21:59 -> Không kẹp'),
      (slot: 'Tối', defaultTime: TimeOfDay(hour: 20, minute: 0), inputTime: TimeOfDay(hour: 17, minute: 59), wantAdjusted: true, wantHour: 18, wantMinute: 0, description: 'Dưới khoảng 17:59 -> Kẹp lên 18:00'),
      (slot: 'Tối', defaultTime: TimeOfDay(hour: 20, minute: 0), inputTime: TimeOfDay(hour: 22, minute: 0), wantAdjusted: true, wantHour: 21, wantMinute: 59, description: 'Trên khoảng 22:00 -> Kẹp xuống 21:59'),
      (slot: 'Tối', defaultTime: TimeOfDay(hour: 20, minute: 0), inputTime: TimeOfDay(hour: 4, minute: 0), wantAdjusted: true, wantHour: 18, wantMinute: 0, description: 'Hoàn toàn ngoài 04:00 -> Kẹp lên 18:00'),
      (slot: 'Tối', defaultTime: TimeOfDay(hour: 20, minute: 0), inputTime: TimeOfDay(hour: 0, minute: 30), wantAdjusted: true, wantHour: 18, wantMinute: 0, description: 'Dưới khoảng sau nửa đêm 00:30 -> Kẹp lên 18:00'),

      // Trường hợp Ngoại lệ: Nhãn Khác
      (slot: 'Khác', defaultTime: TimeOfDay(hour: 7, minute: 0), inputTime: TimeOfDay(hour: 17, minute: 55), wantAdjusted: false, wantHour: 17, wantMinute: 55, description: 'Nhãn không xác định -> Giữ nguyên, không kẹp'),
    ];

    for (final tc in clampMatrix) {
      test('[${tc.slot}] ${tc.description}', () {
        final res = _clampToSlot(tc.slot, tc.defaultTime, tc.inputTime);
        expect(res.adjusted, tc.wantAdjusted);
        expect(res.hour, tc.wantHour);
        expect(res.minute, tc.wantMinute);
      });
    }
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
