import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:workmanager/workmanager.dart';

import '../../../../shared/providers/app_providers.dart';

/// WorkManager background task name — must match AndroidManifest declaration.
const widgetSyncTaskName = 'com.adsus.adsus_mobile.WIDGET_SYNC';

/// WorkManager periodic callback dispatcher.
///
/// T-3.1 — ADSUS Medication Widget
@pragma('vm:entry-point')
void adsusCallbackDispatcher() {
  Workmanager().executeTask((task, inputData) async {
    if (task == widgetSyncTaskName) {
      // NOTE: Cannot access Riverpod/Flutter here — runs in a background isolate.
      // For auth token, read from flutter_secure_storage via home_widget helper.
      return true;
    }
    return false;
  });
}

/// Service that manages widget background sync via WorkManager.
///
/// Syncs are triggered:
/// - Automatically every 15 minutes via WorkManager periodic task
/// - Manually on app open (T-3.3)
/// - After intake confirmation (T-6.2)
class WidgetSyncService {
  const WidgetSyncService(this._ref);

  final Ref _ref;

  /// Registers the background task with WorkManager and schedules periodic sync.
  Future<void> initialize() async {
    await Workmanager().registerPeriodicTask(
      widgetSyncTaskName,
      widgetSyncTaskName,
      frequency: const Duration(minutes: 15),
      constraints: Constraints(
        networkType: NetworkType.connected,
      ),
      inputData: {},
    );
  }

  /// Triggers an immediate widget sync — call when:
  /// - App opens (T-3.3)
  /// - User confirms an intake (T-6.2)
  Future<void> triggerSync() async {
    try {
      final repo = _ref.read(widgetDataRepositoryProvider);
      await repo.syncTodayDoses();
    } catch (e) {
      debugPrint('[WidgetSyncService] sync failed: $e');
    }
  }

  /// Cancels all scheduled background tasks.
  Future<void> cancelAll() async {
    await Workmanager().cancelByUniqueName(widgetSyncTaskName);
  }
}

/// Provider for WidgetSyncService (defined here, re-exported via app_providers.dart).
final widgetSyncServiceProvider = Provider<WidgetSyncService>((ref) {
  return WidgetSyncService(ref);
});
