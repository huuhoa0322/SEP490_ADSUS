import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Cài đặt nhắc uống thuốc cá nhân.
///
/// Stub: lưu local trong SharedPreferences.
/// TODO(capstone-extension): khi backend có PatientReminderPreferenceController (GET/PUT)
/// thì đổi thành load từ API + save lên server thay vì chỉ local.
class ReminderPreference {
  const ReminderPreference({
    this.notifEnabled = true,
    this.morningTime = const TimeOfDay(hour: 7, minute: 0),
    this.middayTime = const TimeOfDay(hour: 12, minute: 0),
    this.eveningTime = const TimeOfDay(hour: 20, minute: 0),
  });

  final bool notifEnabled;
  final TimeOfDay morningTime;
  final TimeOfDay middayTime;
  final TimeOfDay eveningTime;

  ReminderPreference copyWith({
    bool? notifEnabled,
    TimeOfDay? morningTime,
    TimeOfDay? middayTime,
    TimeOfDay? eveningTime,
  }) =>
      ReminderPreference(
        notifEnabled: notifEnabled ?? this.notifEnabled,
        morningTime: morningTime ?? this.morningTime,
        middayTime: middayTime ?? this.middayTime,
        eveningTime: eveningTime ?? this.eveningTime,
      );

  /// Định dạng giờ theo HH:mm.
  String _fmt(TimeOfDay t) =>
      '${t.hour.toString().padLeft(2, '0')}:${t.minute.toString().padLeft(2, '0')}';

  Map<String, String> toMap() => {
        'notifEnabled': notifEnabled.toString(),
        'morningTime': _fmt(morningTime),
        'middayTime': _fmt(middayTime),
        'eveningTime': _fmt(eveningTime),
      };

  static ReminderPreference fromMap(Map<String, String> m) {
    TimeOfDay defaultFor(String key) {
      switch (key) {
        case 'morningTime': return const TimeOfDay(hour: 7, minute: 0);
        case 'middayTime': return const TimeOfDay(hour: 12, minute: 0);
        case 'eveningTime': return const TimeOfDay(hour: 20, minute: 0);
        default: return const TimeOfDay(hour: 7, minute: 0);
      }
    }

    TimeOfDay parseTime(String key) {
      final v = m[key] ?? '';
      final parts = v.split(':');
      if (parts.length != 2) return defaultFor(key);
      final h = int.tryParse(parts[0]);
      final min = int.tryParse(parts[1]);
      if (h == null || min == null) return defaultFor(key);
      return TimeOfDay(hour: h, minute: min);
    }

    return ReminderPreference(
      notifEnabled: m['notifEnabled'] == 'true',
      morningTime: parseTime('morningTime'),
      middayTime: parseTime('middayTime'),
      eveningTime: parseTime('eveningTime'),
    );
  }
}

class ReminderPreferenceNotifier extends AsyncNotifier<ReminderPreference> {
  static const _key = 'patient_reminder_preference';

  @override
  Future<ReminderPreference> build() async {
    final prefs = await SharedPreferences.getInstance();
    final map = <String, String>{};
    for (final k in ['notifEnabled', 'morningTime', 'middayTime', 'eveningTime']) {
      map[k] = prefs.getString('$_key.$k') ?? '';
    }
    return ReminderPreference.fromMap(map);
  }

  Future<void> _save(ReminderPreference p) async {
    final prefs = await SharedPreferences.getInstance();
    final map = p.toMap();
    for (final entry in map.entries) {
      await prefs.setString('$_key.${entry.key}', entry.value);
    }
  }

  Future<void> setNotifEnabled(bool value) async {
    final current = state.valueOrNull ?? const ReminderPreference();
    final next = current.copyWith(notifEnabled: value);
    state = AsyncValue.data(next);
    await _save(next);
  }

  Future<void> setMorningTime(TimeOfDay time) async {
    final current = state.valueOrNull ?? const ReminderPreference();
    final next = current.copyWith(morningTime: time);
    state = AsyncValue.data(next);
    await _save(next);
  }

  Future<void> setMiddayTime(TimeOfDay time) async {
    final current = state.valueOrNull ?? const ReminderPreference();
    final next = current.copyWith(middayTime: time);
    state = AsyncValue.data(next);
    await _save(next);
  }

  Future<void> setEveningTime(TimeOfDay time) async {
    final current = state.valueOrNull ?? const ReminderPreference();
    final next = current.copyWith(eveningTime: time);
    state = AsyncValue.data(next);
    await _save(next);
  }
}

final reminderPreferenceProvider =
    AsyncNotifierProvider<ReminderPreferenceNotifier, ReminderPreference>(
        ReminderPreferenceNotifier.new);
