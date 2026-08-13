import 'package:flutter/foundation.dart';

import '../../domain/entities/appointment.dart';
import '../../domain/entities/appointment_summary.dart';
import '../../domain/entities/schedule_slot.dart';
import '../dtos/appointment_dtos.dart';

/// Chuyển JSON từ tầng data sang entity của tầng domain.
///
/// Toàn bộ quy tắc mã hoá UPPERCASE Postgres → lowerCamelCase Dart enum được chôn ở
/// đây, để phía trên không bao giờ thấy "BOOKED" hay "OPEN".
class AppointmentMapper {
  const AppointmentMapper._();

  /// Backend trả `slotDate` dạng ISO-8601; cắt phần thời gian đi vì các slot đều bắt
  /// đầu lúc 00:00 giờ local — phần giờ thật nằm ở `startTime`.
  static DateTime? parseSlotDate(String? raw) {
    if (raw == null || raw.isEmpty) return null;
    final dt = DateTime.tryParse(raw);
    if (dt == null) return null;
    return DateTime(dt.year, dt.month, dt.day);
  }

  /// Backend trả giờ dạng "HH:mm:ss" hoặc "HH:mm". Trả về "HH:mm" cho UI đồng nhất.
  static String? parseHm(String? raw) {
    if (raw == null || raw.isEmpty) return null;
    final parts = raw.split(':');
    if (parts.length < 2) return raw;
    return '${parts[0]}:${parts[1]}';
  }

  static SlotStatus parseSlotStatus(dynamic raw) {
    // Backend trả int (0=OPEN, 1=CLOSED), JSON parser convert sang string "0"/"1".
    if (raw is int) {
      return raw == 0 ? SlotStatus.open : SlotStatus.closed;
    }
    switch (raw?.toString()) {
      case '0':
        return SlotStatus.open;
      case '1':
        return SlotStatus.closed;
      case 'OPEN':
        return SlotStatus.open;
      case 'CLOSED':
        return SlotStatus.closed;
      default:
        return SlotStatus.open;
    }
  }

  static AppointmentStatus parseAppointmentStatus(dynamic raw) {
    // Backend trả enum dưới dạng string (JsonStringEnumConverter): "Booked" / "Cancelled" (camelCase)
    // Hoặc int cũ (0=BOOKED, 1=CANCELLED) cho backward compatibility.
    debugPrint('[DEBUG Mapper] parseAppointmentStatus: raw=$raw (type: ${raw?.runtimeType})');

    if (raw is int) {
      final result = raw == 0 ? AppointmentStatus.booked : AppointmentStatus.cancelled;
      debugPrint('[DEBUG Mapper] parseAppointmentStatus: int case → $result');
      return result;
    }
    final str = raw?.toString();
    switch (str) {
      // Int string
      case '0':
        debugPrint('[DEBUG Mapper] parseAppointmentStatus: case "0" → booked');
        return AppointmentStatus.booked;
      case '1':
        debugPrint('[DEBUG Mapper] parseAppointmentStatus: case "1" → cancelled');
        return AppointmentStatus.cancelled;
      // UPPERCASE (legacy backend)
      case 'BOOKED':
        debugPrint('[DEBUG Mapper] parseAppointmentStatus: case "BOOKED" → booked');
        return AppointmentStatus.booked;
      case 'CANCELLED':
        debugPrint('[DEBUG Mapper] parseAppointmentStatus: case "CANCELLED" → cancelled');
        return AppointmentStatus.cancelled;
      // camelCase (current backend - JsonStringEnumConverter)
      case 'Booked':
        debugPrint('[DEBUG Mapper] parseAppointmentStatus: case "Booked" → booked');
        return AppointmentStatus.booked;
      case 'Cancelled':
        debugPrint('[DEBUG Mapper] parseAppointmentStatus: case "Cancelled" → cancelled');
        return AppointmentStatus.cancelled;
      default:
        debugPrint('[WARN Mapper] parseAppointmentStatus: Unknown status "$str" → defaulting to booked');
        return AppointmentStatus.booked;
    }
  }

  static ScheduleSlot slotFromDto(ScheduleSlotDto dto) => ScheduleSlot(
        id: dto.slotId ?? '',
        doctorId: dto.doctorId ?? '',
        doctorName: dto.doctorName ?? '',
        slotDate: parseSlotDate(dto.slotDate) ?? DateTime.now(),
        startTime: parseHm(dto.startTime) ?? '',
        endTime: parseHm(dto.endTime) ?? '',
        status: parseSlotStatus(dto.status),
      );

  static Appointment appointmentFromDto(AppointmentDto dto) => Appointment(
        id: dto.appointmentId ?? '',
        slotId: dto.slotId ?? '',
        patientProfileId: dto.patientProfileId ?? '',
        reason: dto.reason,
        status: parseAppointmentStatus(dto.status),
        cancelledReason: dto.cancelledReason,
        createdAt: DateTime.tryParse(dto.createdAt ?? '') ?? DateTime.now(),
        updatedAt: DateTime.tryParse(dto.updatedAt ?? '') ?? DateTime.now(),
        slotDate: parseSlotDate(dto.slotDate),
        startTime: parseHm(dto.startTime),
        endTime: parseHm(dto.endTime),
        doctorName: dto.doctorName,
      );

  static AppointmentSummary summaryFromDto(AppointmentSummaryDto dto) =>
      AppointmentSummary(
        id: dto.appointmentId ?? '',
        slotId: dto.slotId ?? '',
        status: parseAppointmentStatus(dto.status),
        reason: dto.reason,
        cancelledReason: dto.cancelledReason,
        createdAt: DateTime.tryParse(dto.createdAt ?? '') ?? DateTime.now(),
        slotDate: parseSlotDate(dto.slotDate),
        startTime: parseHm(dto.startTime),
        endTime: parseHm(dto.endTime),
        doctorId: dto.doctorId,
        doctorName: dto.doctorName,
      );
}
