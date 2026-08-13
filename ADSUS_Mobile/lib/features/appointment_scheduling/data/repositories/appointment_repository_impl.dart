import 'package:dio/dio.dart';

import '../../../../core/constants/api_constants.dart';
import '../../../../core/network/api_exception.dart';
import '../../domain/entities/appointment.dart';
import '../../domain/entities/appointment_summary.dart';
import '../../domain/entities/schedule_slot.dart';
import '../../domain/repositories/appointment_repository.dart';
import '../dtos/appointment_dtos.dart';
import '../mappers/appointment_mapper.dart';

/// Triển khai gọi API thật cho module Đặt lịch (UC-13, UC-14).
///
/// Chỉ có duy nhất chỗ này được `import 'package:dio/dio.dart'` trong cả module — domain
/// và presentation tầng trên hoàn toàn không biết `dio` tồn tại.
class AppointmentRepositoryImpl implements AppointmentRepository {
  AppointmentRepositoryImpl(this._dio);

  final Dio _dio;

  @override
  Future<List<ScheduleSlot>> searchOpenSlots({
    String? doctorId,
    DateTime? slotDate,
  }) async {
    try {
      final query = <String, dynamic>{
        // UC-13 BR-02: bệnh nhân chỉ đặt được slot Open, tầng data áp luôn.
        'status': 'OPEN',
        // Mặc định pageSize=100 — danh sách slot hiếm khi quá vài chục; UC chưa yêu cầu
        // "load more" vô tận.
        'pageSize': 100,
      };
      if (doctorId != null && doctorId.isNotEmpty) {
        query['doctorId'] = doctorId;
      }
      if (slotDate != null) {
        query['slotDate'] = _formatDate(slotDate);
      }

      final res = await _dio.get<Map<String, dynamic>>(
        ApiConstants.appointmentSlots,
        queryParameters: query,
      );

      // Kiểm tra null + type safety trước khi parse
      if (res.data == null) {
        throw ApiException('Response rỗng (status: ${res.statusCode}). Kiểm tra backend.');
      }
      if (res.data is! Map<String, dynamic>) {
        throw ApiException('Response không đúng định dạng: ${res.data.runtimeType}.');
      }

      final envelope = ApiEnvelope.fromJson(res.data!);
      if (envelope.code != 200) {
        throw ApiException(envelope.message);
      }
      if (envelope.data == null) {
        throw const ApiException('Không tải được danh sách khung giờ.');
      }

      // Backend trả List trực tiếp trong "data", không phải PagedResult.
      List<ScheduleSlotDto> slotList;
      if (envelope.data is List) {
        slotList = (envelope.data as List)
            .map((e) {
              if (e is! Map<String, dynamic>) {
                throw ApiException('Slot item không đúng định dạng: ${e.runtimeType}');
              }
              return ScheduleSlotDto.fromJson(e);
            })
            .toList();
      } else {
        final paged = PagedResultDto.fromJson(envelope.data as Map<String, dynamic>, ScheduleSlotDto.fromJson);
        slotList = paged.items;
      }
      final slots = slotList
          .map(AppointmentMapper.slotFromDto)
          .where((s) => s.status == SlotStatus.open)
          .toList()
        ..sort((a, b) => a.startAt.compareTo(b.startAt));
      return slots;
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e, fallback: 'Không tải được danh sách khung giờ.');
    }
  }

  @override
  Future<Appointment> bookAppointment({
    required String scheduleSlotId,
    String? reason,
  }) async {
    try {
      final body = <String, dynamic>{
        'scheduleSlotId': scheduleSlotId,
      };
      // BR-03: reason là optional, chỉ gửi khi người dùng nhập — không gửi chuỗi rỗng.
      if (reason != null && reason.trim().isNotEmpty) {
        body['reason'] = reason.trim();
      }

      final res = await _dio.post<Map<String, dynamic>>(
        ApiConstants.appointments,
        data: body,
      );

      final envelope = ApiEnvelope.fromJson(res.data ?? const {});
      if (envelope.data == null) {
        throw const ApiException('Đặt lịch thất bại.');
      }
      return AppointmentMapper.appointmentFromDto(
        AppointmentDto.fromJson(envelope.data!),
      );
    } on DioException catch (e) {
      // 409 = đã có booking trùng (UC-13 AF-01 BR-01); 422 = slot không còn OPEN.
      throw ApiErrorMapper.general(e, fallback: 'Đặt lịch thất bại.');
    }
  }

  @override
  Future<List<AppointmentSummary>> listMyAppointments({
    AppointmentStatus? status,
    int page = 1,
    int pageSize = 50,
  }) async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(
        ApiConstants.appointments,
      );

      // Kiểm tra null + type safety
      if (res.data == null) {
        throw ApiException('Response rỗng (status: ${res.statusCode}).');
      }
      if (res.data is! Map<String, dynamic>) {
        throw ApiException('Response không đúng định dạng: ${res.data.runtimeType}.');
      }

      final envelope = ApiEnvelope.fromJson(res.data!);
      if (envelope.code != 200) {
        throw ApiException(envelope.message);
      }
      if (envelope.data == null) {
        throw const ApiException('Không tải được danh sách cuộc hẹn.');
      }

      // Backend trả List trực tiếp, không phải PagedResult.
      List<AppointmentSummaryDto> apptList;
      if (envelope.data is List) {
        apptList = (envelope.data as List)
            .map((e) {
              if (e is! Map<String, dynamic>) {
                throw ApiException('Appointment item không đúng định dạng.');
              }
              return AppointmentSummaryDto.fromJson(e);
            })
            .toList();
      } else {
        final paged = PagedResultDto.fromJson(envelope.data as Map<String, dynamic>, AppointmentSummaryDto.fromJson);
        apptList = paged.items;
      }
      return apptList.map(AppointmentMapper.summaryFromDto).toList();
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e, fallback: 'Không tải được danh sách cuộc hẹn.');
    }
  }

  @override
  Future<Appointment> getMyAppointment(String id) async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(
        '${ApiConstants.appointments}/$id',
      );
      final envelope = ApiEnvelope.fromJson(res.data ?? const {});
      if (envelope.data == null) {
        throw const ApiException('Không tải được chi tiết cuộc hẹn.');
      }
      return AppointmentMapper.appointmentFromDto(
        AppointmentDto.fromJson(envelope.data!),
      );
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e, fallback: 'Không tải được chi tiết cuộc hẹn.');
    }
  }

  @override
  Future<Appointment> cancelMyAppointment({
    required String id,
    required String cancellationReason,
  }) async {
    // BR-02: lý do hủy BẮT BUỘC. Kiểm tra sớm ở đây để khỏi tốn request vô ích — backend
    // cũng kiểm tra nhưng vòng vèo vài trăm ms + hiển thị sai thông báo.
    if (cancellationReason.trim().isEmpty) {
      throw const ApiException('Vui lòng chọn lý do hủy trước khi xác nhận.');
    }

    try {
      final res = await _dio.post<Map<String, dynamic>>(
        ApiConstants.cancelAppointment(id),
        data: {'cancellationReason': cancellationReason.trim()},
      );

      final envelope = ApiEnvelope.fromJson(res.data ?? const {});
      if (envelope.data == null) {
        throw const ApiException('Hủy lịch thất bại.');
      }
      return AppointmentMapper.appointmentFromDto(
        AppointmentDto.fromJson(envelope.data!),
      );
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e, fallback: 'Hủy lịch thất bại.');
    }
  }

  static String _formatDate(DateTime d) =>
      '${d.year.toString().padLeft(4, '0')}-'
      '${d.month.toString().padLeft(2, '0')}-'
      '${d.day.toString().padLeft(2, '0')}';
}
