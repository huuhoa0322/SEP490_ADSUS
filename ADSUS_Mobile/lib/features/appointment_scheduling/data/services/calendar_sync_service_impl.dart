import 'package:shared_preferences/shared_preferences.dart';

import '../../domain/entities/appointment.dart';
import '../../domain/services/calendar_sync_service.dart';

/// Triển khai [CalendarSyncService] - STUB VERSION.
///
/// NOTE: Plugin `add_2_calendar` bị lỗi Kotlin cache trên Windows.
/// Phiên bản này chỉ lưu cờ local, không thực sự thêm vào Calendar.
/// Khi add_2_calendar được enable lại, thay thế bằng implementation thật.
class CalendarSyncServiceImpl implements CalendarSyncService {
  CalendarSyncServiceImpl({required SharedPreferences prefs})
      : _prefs = prefs;

  final SharedPreferences _prefs;

  static const _prefix = 'synced_';

  @override
  Future<bool> addAppointmentToCalendar(Appointment appointment) async {
    // Stub: chỉ lưu cờ local, không thêm vào Calendar thật
    if (appointment.slotDate == null) {
      throw const CalendarSyncException(
        'Không có ngày khám — không thể thêm vào lịch. '
        'Vui lòng mở chi tiết cuộc hẹn trước.',
      );
    }

    // Lưu cờ để UI hiển thị icon đã sync
    await _prefs.setBool('$_prefix${appointment.id}', true);

    // Stub luôn trả về true để simulate thành công
    return true;
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
