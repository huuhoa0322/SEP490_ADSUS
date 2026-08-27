/// DTO khớp 1:1 với `ScheduleSlotResponse` từ backend (api spec #46, #47, #48).
///
/// Tất cả trường là nullable để chịu được response thiếu — đặc biệt `doctorName` vì
/// backend chưa chốt chắc có nhúng tên bác sĩ vào response của mobile hay không. Tầng
/// mapper sẽ quyết định fallback khi thiếu.
class ScheduleSlotDto {
  final String? slotId;
  final String? doctorId;
  final String? doctorName;
  final String? slotDate;

  /// "HH:mm:ss" hoặc "HH:mm" — backend có thể trả cả hai.
  final String? startTime;
  final String? endTime;

  /// Backend enum: "OPEN" hoặc "CLOSED". Theo UCS, không bao giờ là "FULL".
  final String? status;

  /// Trạng thái tài khoản bác sĩ: "ACTIVE", "INACTIVE", hoặc int (0/1).
  final String? doctorStatus;

  final String? createdAt;
  final String? updatedAt;

  ScheduleSlotDto({
    this.slotId,
    this.doctorId,
    this.doctorName,
    this.slotDate,
    this.startTime,
    this.endTime,
    this.status,
    this.doctorStatus,
    this.createdAt,
    this.updatedAt,
  });

  factory ScheduleSlotDto.fromJson(Map<String, dynamic> json) => ScheduleSlotDto(
        slotId: json['slotId'] as String?,
        doctorId: json['doctorId'] as String?,
        doctorName: json['doctorName'] as String?,
        slotDate: json['slotDate'] as String?,
        startTime: json['startTime'] as String?,
        endTime: json['endTime'] as String?,
        // Backend trả int (0=OPEN, 1=CLOSED), convert sang string.
        status: json['status']?.toString(),
        // Backend trả doctorStatus: "ACTIVE"/"INACTIVE" hoặc int (0/1).
        doctorStatus: json['doctorStatus']?.toString(),
        createdAt: json['createdAt'] as String?,
        updatedAt: json['updatedAt'] as String?,
      );
}

/// DTO khớp với `AppointmentResponse` (#50, #51, #52).
class AppointmentDto {
  final String? appointmentId;
  final String? slotId;
  final String? patientProfileId;
  final String? reason;
  final String? status;
  final String? cancelledReason;
  final String? calendarSyncedAt;
  final String? createdAt;
  final String? updatedAt;

  // Backend có thể nhúng thông tin slot (xem ucs report 3.1 §Postconditions).
  final String? slotDate;
  final String? startTime;
  final String? endTime;
  final String? doctorName;

  // Case được tạo từ booking (nếu có triệu chứng)
  final String? caseId;

  AppointmentDto({
    this.appointmentId,
    this.slotId,
    this.patientProfileId,
    this.reason,
    this.status,
    this.cancelledReason,
    this.calendarSyncedAt,
    this.createdAt,
    this.updatedAt,
    this.slotDate,
    this.startTime,
    this.endTime,
    this.doctorName,
    this.caseId,
  });

  factory AppointmentDto.fromJson(Map<String, dynamic> json) => AppointmentDto(
        appointmentId: json['appointmentId'] as String?,
        slotId: json['slotId'] as String?,
        patientProfileId: json['patientProfileId'] as String?,
        reason: json['reason'] as String?,
        // Backend trả int (0=BOOKED, 1=CANCELLED), toString() để convert thành string.
        status: json['status']?.toString(),
        cancelledReason: json['cancelledReason'] as String?,
        calendarSyncedAt: json['calendarSyncedAt'] as String?,
        createdAt: json['createdAt'] as String?,
        updatedAt: json['updatedAt'] as String?,
        slotDate: json['slotDate'] as String?,
        startTime: json['startTime'] as String?,
        endTime: json['endTime'] as String?,
        doctorName: json['doctorName'] as String?,
        caseId: json['caseId'] as String?,
      );
}

/// DTO khớp với `AppointmentSummaryResponse` (#49, #53).
class AppointmentSummaryDto {
  final String? appointmentId;
  final String? slotId;
  final String? patientProfileId;
  final String? status;
  final String? reason;
  final String? cancelledReason;
  final String? slotDate;
  final String? startTime;
  final String? endTime;
  final String? doctorId;
  final String? doctorName;
  final String? createdAt;

  AppointmentSummaryDto({
    this.appointmentId,
    this.slotId,
    this.patientProfileId,
    this.status,
    this.reason,
    this.cancelledReason,
    this.slotDate,
    this.startTime,
    this.endTime,
    this.doctorId,
    this.doctorName,
    this.createdAt,
  });

  factory AppointmentSummaryDto.fromJson(Map<String, dynamic> json) {
    final statusRaw = json['status'];
    // ignore: avoid_print
    print('[DEBUG DTO] AppointmentSummaryDto.fromJson: statusRaw=$statusRaw (type: ${statusRaw?.runtimeType})');
    return AppointmentSummaryDto(
      appointmentId: json['appointmentId'] as String?,
      slotId: json['slotId'] as String?,
      patientProfileId: json['patientProfileId'] as String?,
      // Backend trả int (0=BOOKED, 1=CANCELLED), convert sang string.
      status: statusRaw?.toString(),
      reason: json['reason'] as String?,
      cancelledReason: json['cancelledReason'] as String?,
      slotDate: json['slotDate'] as String?,
      startTime: json['startTime'] as String?,
      endTime: json['endTime'] as String?,
      doctorId: json['doctorId'] as String?,
      doctorName: json['doctorName'] as String?,
      createdAt: json['createdAt'] as String?,
    );
  }
}

/// Vỏ bọc `{code, message, data}` của mọi response từ ADSUS_BE.
///
/// Đã có `ApiEnvelope` ở tầng auth nhưng trùng code này dễ phải đụng auth. Tách riêng
/// cho module này để đổi lúc nào cũng chỉ một mình, không lan sang module khác.
///
/// FIX: `data` phải là `dynamic` vì backend trả cả `List` (cho endpoints danh sách)
/// lẫn `Map` (cho responses đơn lẻ). Cast sang `Map` cứng sẽ fail → data = null.
class ApiEnvelope {
  const ApiEnvelope({required this.code, required this.message, this.data});

  final int code;
  final String message;

  /// Có thể là `List<dynamic>` (danh sách slot/appointment) hoặc `Map<String, dynamic>` (chi tiết).
  final dynamic data;

  factory ApiEnvelope.fromJson(Map<String, dynamic> json) => ApiEnvelope(
        code: json['code'] as int? ?? 0,
        message: json['message'] as String? ?? '',
        // Dynamic cast — không ép Map để nhận cả List lẫn Map.
        data: json['data'],
      );
}

/// Vỏ phân trang chuẩn của backend — `PagedResult<T>`.
///
/// Module 8 trả danh sách có phân trang (#47, #49, #53) theo Flag F2 của spec.
class PagedResultDto<T> {
  const PagedResultDto({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalItems,
    required this.totalPages,
  });

  final List<T> items;
  final int page;
  final int pageSize;
  final int totalItems;
  final int totalPages;

  static PagedResultDto<T> fromItems<T>(
    List<T> items, {
    int page = 1,
    int pageSize = 50,
  }) {
    return PagedResultDto<T>(
      items: items,
      page: page,
      pageSize: pageSize,
      totalItems: items.length,
      totalPages: 1,
    );
  }

  factory PagedResultDto.fromJson(
    Map<String, dynamic> json,
    T Function(Map<String, dynamic>) itemBuilder,
  ) {
    final items = (json['items'] as List?)
            ?.cast<Map<String, dynamic>>()
            .map(itemBuilder)
            .toList() ??
        const <Never>[];
    return PagedResultDto<T>(
      items: items,
      page: json['page'] as int? ?? 1,
      pageSize: json['pageSize'] as int? ?? items.length,
      totalItems: json['totalItems'] as int? ?? items.length,
      totalPages: json['totalPages'] as int? ?? 1,
    );
  }
}
