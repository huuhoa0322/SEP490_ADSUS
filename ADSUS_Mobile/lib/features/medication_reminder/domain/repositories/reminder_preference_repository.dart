import '../../data/dtos/reminder_preference_dto.dart';

/// Interface cho reminder preference (SCR-19).
///
/// Backend: GET/PUT /api/v1/me/reminder-preference.
/// Stub SharedPreferences được dùng tạm trước khi có backend (hiện tại).
abstract class ReminderPreferenceRepository {
  Future<ReminderPreferenceData> get();
  Future<ReminderPreferenceData> upsert({
    bool? notifEnabled,
    String? morningTime,
    String? middayTime,
    String? eveningTime,
  });
}
