import 'package:add_2_calendar/add_2_calendar.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../domain/entities/appointment.dart';
import '../../domain/services/calendar_sync_service.dart';

/// Triển khai [CalendarSyncService] — dùng plugin `add_2_calendar`.
///
/// Gọi OS-level Calendar API để thêm event vào Calendar mặc định hoặc bất kỳ
/// calendar app nào user chọn trên thiết bị (Google Calendar, Samsung Calendar,
/// Outlook, v.v.). OS hiện dialog chọn app và tự xử lý quyền — không cần
/// runtime permission từ phía app.
///
/// Tạo 3 events:
/// 1. Event chính: thông tin lịch khám đầy đủ
/// 2. Reminder 24h: nhắc trước 24 giờ
/// 3. Reminder 1h: nhắc trước 1 giờ
class CalendarSyncServiceImpl implements CalendarSyncService {
  CalendarSyncServiceImpl({required this._prefs});

  final SharedPreferences _prefs;
  static const _prefix = 'synced_';

  @override
  Future<bool> addAppointmentToCalendar(Appointment appointment) async {
    if (appointment.slotDate == null || appointment.startTime == null) {
      throw const CalendarSyncException(
        'Không đủ thông tin ngày/giờ để thêm vào lịch. '
        'Vui lòng mở chi tiết cuộc hẹn trước.',
      );
    }

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

    // 1. Event chính - thông tin lịch khám đầy đủ
    final mainEvent = Event(
      title: 'Lịch khám ADSUS: $doctorTitle',
      description: _buildDescription(appointment),
      location: 'Phòng khám ADSUS',
      startDate: startDateTime,
      endDate: endDateTime,
      allDay: false,
    );

    // 2. Reminder 24h trước giờ khám
    final reminder24hTime = startDateTime.subtract(const Duration(hours: 24));
    final reminder24h = Event(
      title: '🔔 NHẮC LỊCH: 24h nữa khám $doctorTitle',
      description: 'Nhắc lịch khám ADSUS vào lúc ${_formatDateTime(startDateTime)}',
      startDate: reminder24hTime,
      endDate: reminder24hTime.add(const Duration(minutes: 30)),
      allDay: false,
    );

    // 3. Reminder 1h trước giờ khám
    final reminder1hTime = startDateTime.subtract(const Duration(hours: 1));
    final reminder1h = Event(
      title: '🔔 NHẮC LỊCH: 1h nữa khám $doctorTitle',
      description: 'Nhắc lịch khám ADSUS vào lúc ${_formatDateTime(startDateTime)}',
      startDate: reminder1hTime,
      endDate: reminder1hTime.add(const Duration(minutes: 30)),
      allDay: false,
    );

    // Gọi lần lượt - user sẽ thấy 3 dialog calendar
    final result1 = await Add2Calendar.addEvent2Cal(mainEvent);
    final result2 = await Add2Calendar.addEvent2Cal(reminder24h);
    final result3 = await Add2Calendar.addEvent2Cal(reminder1h);

    // Thành công nếu ít nhất 1 event được thêm
    final anySuccess = result1 || result2 || result3;
    if (anySuccess) {
      await _prefs.setBool('$_prefix${appointment.id}', true);
    }

    return anySuccess;
  }

  String _buildDescription(Appointment appointment) {
    if (appointment.reason != null && appointment.reason!.isNotEmpty) {
      return 'Lý do khám: ${appointment.reason}';
    }
    return 'Lịch khám bệnh qua ứng dụng ADSUS';
  }

  String _formatDateTime(DateTime dt) {
    return '${dt.day.toString().padLeft(2, '0')}/'
        '${dt.month.toString().padLeft(2, '0')}/${dt.year} '
        '${dt.hour.toString().padLeft(2, '0')}:${dt.minute.toString().padLeft(2, '0')}';
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

  @override
  Future<bool> hasSynced(String appointmentId) async {
    return _prefs.getBool('$_prefix$appointmentId') ?? false;
  }

  @override
  Future<void> clearSyncFlag(String appointmentId) async {
    await _prefs.remove('$_prefix$appointmentId');
  }
}
