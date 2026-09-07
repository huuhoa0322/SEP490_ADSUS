import 'dart:convert';

import 'package:home_widget/home_widget.dart';

import '../models/today_dose.dart';
import '../../domain/entities/intake_log.dart';
import '../../domain/repositories/medication_intake_repository.dart';

/// Widget data repository — reads all intakes, filters today's pending doses,
/// and serializes to JSON for Android widget consumption via home_widget.
///
/// T-2.2 — ADSUS Medication Widget
class WidgetDataRepository {
  static const _widgetDataKey = 'widget_data';

  const WidgetDataRepository(this._intakeRepo);

  final MedicationIntakeRepository _intakeRepo;

  /// Fetches today's doses from the backend, filters to today's,
  /// and writes JSON to SharedPreferences for the widget.
  ///
  /// Returns the list of [TodayDose] that were written (for verification).
  Future<List<TodayDose>> syncTodayDoses() async {
    final repo = _intakeRepo;

    // Write "loading" sentinel while fetching
    await _writeWidgetData('loading');

    try {
      final allIntakes = await repo.getMyIntakeLogs();
      final todayUtc = DateTime.now().toUtc();
      final startOfDay = DateTime.utc(todayUtc.year, todayUtc.month, todayUtc.day);
      final endOfDay = startOfDay.add(const Duration(days: 1));

      // Filter chỉ lấy liều trong hôm nay
      final allTodayIntakes = allIntakes.where((log) =>
          log.scheduledTimeUtc.isAfter(startOfDay) &&
          log.scheduledTimeUtc.isBefore(endOfDay)).toList();

      // Lọc bỏ liều đã uống — widget chỉ hiện pending + overtime
      final pendingOvertime = allTodayIntakes
          .where((log) => log.status != IntakeStatus.taken)
          .toList();

      // Sắp xếp: overtime trước, rồi pending, theo scheduledTime
      pendingOvertime.sort((a, b) {
        if (a.status != b.status) {
          if (a.status == IntakeStatus.overtime) return -1;
          if (b.status == IntakeStatus.overtime) return 1;
          return 0;
        }
        return a.scheduledTimeUtc.compareTo(b.scheduledTimeUtc);
      });

      final todayDoses = pendingOvertime
          .map((log) => TodayDose.fromIntakeLog(log))
          .toList();

      // Phân biệt 2 loại empty state
      if (todayDoses.isEmpty) {
        if (allTodayIntakes.isEmpty) {
          // Backend không có intake nào hôm nay → chưa có đơn thuốc
          await _writeWidgetData('no_prescriptions');
        } else {
          // Backend có intake hôm nay nhưng tất cả đều đã uống → Good Job!
          await _writeWidgetData('all_done');
        }
      } else {
        await _writeWidgetDoses(todayDoses);
      }

      return todayDoses;
    } catch (e) {
      await _writeWidgetData('error');
      rethrow;
    }
  }

  /// Write the list of doses as JSON array to SharedPreferences.
  Future<void> _writeWidgetDoses(List<TodayDose> doses) async {
    final jsonList = doses.map((d) => d.toJson()).toList();
    final jsonString = jsonEncode(jsonList);
    await HomeWidget.saveWidgetData<String>(_widgetDataKey, jsonString);
    // Trigger widget update
    await HomeWidget.updateWidget(
      androidName: 'MedicationWidgetProvider',
      iOSName: 'MedicationWidget',
    );
  }

  /// Write a sentinel value (e.g. 'loading', 'error', 'not_logged_in').
  Future<void> _writeWidgetData(String sentinel) async {
    await HomeWidget.saveWidgetData<String>(_widgetDataKey, sentinel);
    await HomeWidget.updateWidget(
      androidName: 'MedicationWidgetProvider',
      iOSName: 'MedicationWidget',
    );
  }

  /// Mark widget as "not logged in" state.
  Future<void> markNotLoggedIn() async {
    await _writeWidgetData('not_logged_in');
  }

  /// Mark widget as "error" state.
  Future<void> markError() async {
    await _writeWidgetData('error');
  }
}
