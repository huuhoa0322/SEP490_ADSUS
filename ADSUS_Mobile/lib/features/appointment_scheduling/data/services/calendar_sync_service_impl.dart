import 'package:device_calendar/device_calendar.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:timezone/timezone.dart' as tz;
import 'package:timezone/data/latest.dart' as tz_data;

import '../../domain/entities/appointment.dart';
import '../../domain/services/calendar_sync_service.dart';

/// Triển khai [CalendarSyncService] — dùng plugin `device_calendar`.
///
/// Gọi OS-level Calendar API để thêm event vào Calendar mặc định hoặc bất kỳ
/// calendar app nào user chọn trên thiết bị (Google Calendar, Samsung Calendar,
/// Outlook, v.v.).
///
/// Tạo 3 events:
/// 1. Event chính: thông tin lịch khám đầy đủ
/// 2. Reminder 24h: nhắc trước 24 giờ
/// 3. Reminder 1h: nhắc trước 1 giờ
class CalendarSyncServiceImpl implements CalendarSyncService {
  CalendarSyncServiceImpl({required this._prefs});

  final SharedPreferences _prefs;
  static const _prefix = 'synced_';

  static bool _initialized = false;

  static void _ensureInitialized() {
    if (!_initialized) {
      tz_data.initializeTimeZones();
      _initialized = true;
    }
  }

  @override
  Future<bool> addAppointmentToCalendar(Appointment appointment) async {
    if (appointment.slotDate == null || appointment.startTime == null) {
      throw const CalendarSyncException(
        'Không đủ thông tin ngày/giờ để thêm vào lịch. '
        'Vui lòng mở chi tiết cuộc hẹn trước.',
      );
    }

    _ensureInitialized();

    final startDateTime = _buildDateTime(
      appointment.slotDate!,
      appointment.startTime!,
    );
    final endDateTime = appointment.endTime != null
        ? _buildDateTime(appointment.slotDate!, appointment.endTime!)
        : startDateTime.add(const Duration(hours: 1));

    final doctorTitle = appointment.doctorName != null
        ? 'BS. ${appointment.doctorName}'
        : 'Bác sĩ';

    // Build description
    String description;
    if (appointment.reason != null && appointment.reason!.isNotEmpty) {
      description = 'Lý do khám: ${appointment.reason}';
    } else {
      description = 'Lịch khám bệnh qua ứng dụng ADSUS';
    }

    // Get device calendars
    final calendarPlugin = DeviceCalendarPlugin();
    final calendarsResult = await calendarPlugin.retrieveCalendars();

    if (calendarsResult.isSuccess && calendarsResult.data != null) {
      // Find primary/default calendar
      final defaultCalendar = calendarsResult.data!.firstWhere(
        (cal) => cal.isDefault == true,
        orElse: () => calendarsResult.data!.first,
      );

      final startTz = tz.TZDateTime.from(startDateTime, tz.local);
      final endTz = tz.TZDateTime.from(endDateTime, tz.local);

      // Create main event
      final mainEvent = Event(
        defaultCalendar.id,
        title: 'Lịch khám ADSUS: $doctorTitle',
        description: description,
        start: startTz,
        end: endTz,
        reminders: [
          Reminder(minutes: 60 * 24), // 24 hours before
          Reminder(minutes: 60), // 1 hour before
        ],
      );

      // Add event to calendar
      final result = await calendarPlugin.createOrUpdateEvent(mainEvent);

      if (result?.isSuccess == true) {
        await _prefs.setBool('$_prefix${appointment.id}', true);
        return true;
      }
    }

    return false;
  }

  @override
  Future<bool> hasSynced(String appointmentId) async {
    return _prefs.getBool('$_prefix$appointmentId') ?? false;
  }

  @override
  Future<void> clearSyncFlag(String appointmentId) async {
    await _prefs.remove('$_prefix$appointmentId');
  }

  DateTime _buildDateTime(DateTime date, String time) {
    final parts = time.split(':');
    return DateTime(
      date.year,
      date.month,
      date.day,
      int.parse(parts[0]),
      parts.length > 1 ? int.parse(parts[1]) : 0,
    );
  }
}
