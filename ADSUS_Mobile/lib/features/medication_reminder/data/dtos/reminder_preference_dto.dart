/// DTO cho PatientReminderPreference (SCR-19).
///
/// Backend trả: { "code": 200, "message": "...", "data": { "notifEnabled": true, ... } }
class ReminderPreferenceEnvelope {
  const ReminderPreferenceEnvelope({required this.data});

  final ReminderPreferenceData? data;

  factory ReminderPreferenceEnvelope.fromJson(Map<String, dynamic>? json) {
    if (json == null) return const ReminderPreferenceEnvelope(data: null);
    return ReminderPreferenceEnvelope(
      data: json['data'] == null
          ? null
          : ReminderPreferenceData.fromJson(json['data'] as Map<String, dynamic>),
    );
  }
}

class ReminderPreferenceData {
  const ReminderPreferenceData({
    required this.notifEnabled,
    required this.morningTime,
    required this.middayTime,
    required this.eveningTime,
  });

  final bool notifEnabled;
  final String morningTime;
  final String middayTime;
  final String eveningTime;

  factory ReminderPreferenceData.fromJson(Map<String, dynamic> json) {
    return ReminderPreferenceData(
      notifEnabled: json['notifEnabled'] as bool,
      morningTime: json['morningTime'] as String,
      middayTime: json['middayTime'] as String,
      eveningTime: json['eveningTime'] as String,
    );
  }
}
