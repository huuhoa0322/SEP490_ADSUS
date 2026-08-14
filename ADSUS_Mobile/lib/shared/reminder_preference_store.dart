import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'providers/app_providers.dart';

/// Cài đặt nhắc uống thuốc cá nhân.
///
/// Backend: GET/PUT /api/v1/me/reminder-preference (PatientReminderPreference).
/// SharedPreferences chỉ dùng làm local cache khi API không khả dụng (offline).
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

  Map<String, String> toMap() => {
        'notifEnabled': notifEnabled.toString(),
        'morningTime': _fmt(morningTime),
        'middayTime': _fmt(middayTime),
        'eveningTime': _fmt(eveningTime),
      };

  static String _fmt(TimeOfDay t) =>
      '${t.hour.toString().padLeft(2, '0')}:${t.minute.toString().padLeft(2, '0')}';
}

class ReminderPreferenceNotifier extends AsyncNotifier<ReminderPreference> {
  static const _keyPrefix = 'patient_reminder_preference';

  String _keyFor(String patientId) => '$_keyPrefix.$patientId';

  /// Cache local: đọc từ SharedPreferences (patientId-keyed).
  Future<ReminderPreference> _loadLocal(String patientId) async {
    final prefs = await SharedPreferences.getInstance();
    final map = <String, String>{};
    for (final k in ['notifEnabled', 'morningTime', 'middayTime', 'eveningTime']) {
      map[k] = prefs.getString('${_keyFor(patientId)}.$k') ?? '';
    }
    return _fromMap(map);
  }

  /// Cache local: ghi xuống SharedPreferences (patientId-keyed).
  Future<void> _saveLocal(String patientId, ReminderPreference p) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString('${_keyFor(patientId)}.notifEnabled', p.notifEnabled.toString());
    await prefs.setString('${_keyFor(patientId)}.morningTime', ReminderPreference._fmt(p.morningTime));
    await prefs.setString('${_keyFor(patientId)}.middayTime', ReminderPreference._fmt(p.middayTime));
    await prefs.setString('${_keyFor(patientId)}.eveningTime', ReminderPreference._fmt(p.eveningTime));
  }

  ReminderPreference _fromMap(Map<String, String> m) {
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

  @override
  Future<ReminderPreference> build() async {
    // Lấy phone từ SecureStorage (pairedPhone) để phân biệt cache giữa các tài khoản.
    // Nếu chưa đăng nhập, dùng placeholder — preferences không hiển thị trước khi login.
    final phone = await ref.read(authRepositoryProvider).readPairedPhone();
    final patientId = phone ?? 'anonymous';

    try {
      // Ưu tiên API backend — đây là nguồn chân lý.
      final repo = ref.read(reminderPreferenceRepositoryProvider);
      final data = await repo.get();

      // Đồng bộ xuống local cache để dùng khi offline.
      final local = ReminderPreference(
        notifEnabled: data.notifEnabled,
        morningTime: _parseApiTime(data.morningTime),
        middayTime: _parseApiTime(data.middayTime),
        eveningTime: _parseApiTime(data.eveningTime),
      );
      await _saveLocal(patientId, local);
      return local;
    } catch (_) {
      // API thất bại (offline) → đọc từ local cache.
      return _loadLocal(patientId);
    }
  }

  Future<void> setNotifEnabled(bool value) async {
    final current = state.valueOrNull ?? const ReminderPreference();
    final next = current.copyWith(notifEnabled: value);
    state = AsyncValue.data(next);
    await _upsertApi(next);
  }

  Future<void> setMorningTime(TimeOfDay time) async {
    final current = state.valueOrNull ?? const ReminderPreference();
    final next = current.copyWith(morningTime: time);
    state = AsyncValue.data(next);
    await _upsertApi(next);
  }

  Future<void> setMiddayTime(TimeOfDay time) async {
    final current = state.valueOrNull ?? const ReminderPreference();
    final next = current.copyWith(middayTime: time);
    state = AsyncValue.data(next);
    await _upsertApi(next);
  }

  Future<void> setEveningTime(TimeOfDay time) async {
    final current = state.valueOrNull ?? const ReminderPreference();
    final next = current.copyWith(eveningTime: time);
    state = AsyncValue.data(next);
    await _upsertApi(next);
  }

  Future<void> _upsertApi(ReminderPreference p) async {
    final phone = await ref.read(authRepositoryProvider).readPairedPhone();
    final patientId = phone ?? 'anonymous';

    try {
      final repo = ref.read(reminderPreferenceRepositoryProvider);
      await repo.upsert(
        notifEnabled: p.notifEnabled,
        morningTime: _fmt(p.morningTime),
        middayTime: _fmt(p.middayTime),
        eveningTime: _fmt(p.eveningTime),
      );
      await _saveLocal(patientId, p);
    } catch (_) {
      // Offline: vẫn lưu local để dùng khi online trở lại.
      await _saveLocal(patientId, p);
    }
  }

  String _fmt(TimeOfDay t) =>
      '${t.hour.toString().padLeft(2, '0')}:${t.minute.toString().padLeft(2, '0')}';

  TimeOfDay _parseApiTime(String api) {
    final parts = api.split(':');
    if (parts.length != 2) return const TimeOfDay(hour: 7, minute: 0);
    final h = int.tryParse(parts[0]);
    final m = int.tryParse(parts[1]);
    if (h == null || m == null) return const TimeOfDay(hour: 7, minute: 0);
    return TimeOfDay(hour: h, minute: m);
  }
}

final reminderPreferenceProvider =
    AsyncNotifierProvider<ReminderPreferenceNotifier, ReminderPreference>(
        ReminderPreferenceNotifier.new);
