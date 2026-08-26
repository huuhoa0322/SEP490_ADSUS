import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/constants/api_constants.dart';
import '../../auth/presentation/viewmodels/auth_view_model.dart';

/// Notification type enum - maps to backend NotificationType
enum NotificationTypeEnum {
  general,
  medicationReminder,
  medicationConfirmation,
  appointmentBooking,
  appointmentReminder,
  appointmentCancellation,
  healthlogReminder,
  medicalRecordAdded,
  blogNewPost,
  weeklyHealthReport,
  adherenceSummary,
}

extension NotificationTypeEnumExtension on NotificationTypeEnum {
  String get value {
    switch (this) {
      case NotificationTypeEnum.general:
        return 'general';
      case NotificationTypeEnum.medicationReminder:
        return 'medication_reminder';
      case NotificationTypeEnum.medicationConfirmation:
        return 'medication_confirmation';
      case NotificationTypeEnum.appointmentBooking:
        return 'appointment_booking';
      case NotificationTypeEnum.appointmentReminder:
        return 'appointment_reminder';
      case NotificationTypeEnum.appointmentCancellation:
        return 'appointment_cancellation';
      case NotificationTypeEnum.healthlogReminder:
        return 'healthlog_reminder';
      case NotificationTypeEnum.medicalRecordAdded:
        return 'medical_record_added';
      case NotificationTypeEnum.blogNewPost:
        return 'blog_new_post';
      case NotificationTypeEnum.weeklyHealthReport:
        return 'weekly_health_report';
      case NotificationTypeEnum.adherenceSummary:
        return 'adherence_summary';
    }
  }

  String get displayName {
    switch (this) {
      case NotificationTypeEnum.general:
        return 'Thông báo chung';
      case NotificationTypeEnum.medicationReminder:
        return 'Nhắc uống thuốc';
      case NotificationTypeEnum.medicationConfirmation:
        return 'Xác nhận uống thuốc';
      case NotificationTypeEnum.appointmentBooking:
        return 'Đặt lịch khám';
      case NotificationTypeEnum.appointmentReminder:
        return 'Nhắc lịch khám';
      case NotificationTypeEnum.appointmentCancellation:
        return 'Hủy lịch khám';
      case NotificationTypeEnum.healthlogReminder:
        return 'Nhắc nhật ký sức khoẻ';
      case NotificationTypeEnum.medicalRecordAdded:
        return 'Hồ sơ y tế mới';
      case NotificationTypeEnum.blogNewPost:
        return 'Bài viết mới';
      case NotificationTypeEnum.weeklyHealthReport:
        return 'Báo cáo sức khoẻ';
      case NotificationTypeEnum.adherenceSummary:
        return 'Tổng kết tuân thủ';
    }
  }

  static NotificationTypeEnum fromString(String? value) {
    if (value == null) return NotificationTypeEnum.general;
    return NotificationTypeEnum.values.firstWhere(
      (e) => e.value == value,
      orElse: () => NotificationTypeEnum.general,
    );
  }
}

/// DTO for notification from API
class NotificationDto {
  final String logId;
  final String type;
  final String title;
  final String? body;
  final String? deepLink;
  final Map<String, dynamic>? metadata;
  final DateTime sentAt;
  final DateTime? readAt;
  final bool isRead;

  NotificationDto({
    required this.logId,
    required this.type,
    required this.title,
    this.body,
    this.deepLink,
    this.metadata,
    required this.sentAt,
    this.readAt,
    required this.isRead,
  });

  NotificationTypeEnum get typeEnum => NotificationTypeEnumExtension.fromString(type);

  /// Get ID from metadata based on notification type
  String? get relatedId {
    if (metadata == null) return null;
    switch (typeEnum) {
      case NotificationTypeEnum.medicationReminder:
      case NotificationTypeEnum.medicationConfirmation:
        return metadata!['scheduleId']?.toString();
      case NotificationTypeEnum.appointmentReminder:
      case NotificationTypeEnum.appointmentBooking:
      case NotificationTypeEnum.appointmentCancellation:
        return metadata!['appointmentId']?.toString();
      case NotificationTypeEnum.medicalRecordAdded:
        return metadata!['recordId']?.toString();
      case NotificationTypeEnum.blogNewPost:
        return metadata!['postId']?.toString();
      default:
        return null;
    }
  }

  factory NotificationDto.fromJson(Map<String, dynamic> json) {
    return NotificationDto(
      logId: json['logId'] as String,
      type: json['type'] as String? ?? 'general',
      title: json['title'] as String,
      body: json['body'] as String?,
      deepLink: json['deepLink'] as String?,
      metadata: json['metadata'] as Map<String, dynamic>?,
      sentAt: DateTime.parse(json['sentAt'] as String),
      readAt: json['readAt'] != null ? DateTime.parse(json['readAt'] as String) : null,
      isRead: json['isRead'] as bool? ?? false,
    );
  }

  NotificationDto copyWith({
    String? logId,
    String? type,
    String? title,
    String? body,
    String? deepLink,
    Map<String, dynamic>? metadata,
    DateTime? sentAt,
    DateTime? readAt,
    bool? isRead,
  }) {
    return NotificationDto(
      logId: logId ?? this.logId,
      type: type ?? this.type,
      title: title ?? this.title,
      body: body ?? this.body,
      deepLink: deepLink ?? this.deepLink,
      metadata: metadata ?? this.metadata,
      sentAt: sentAt ?? this.sentAt,
      readAt: readAt ?? this.readAt,
      isRead: isRead ?? this.isRead,
    );
  }
}

/// State for notifications
class NotificationState {
  const NotificationState({
    this.notifications = const [],
    this.unreadCount = 0,
    this.isLoading = false,
    this.error,
  });

  final List<NotificationDto> notifications;
  final int unreadCount;
  final bool isLoading;
  final String? error;
}

/// Notifier for notifications
class NotificationNotifier extends StateNotifier<NotificationState> {
  NotificationNotifier(this._ref) : super(const NotificationState());

  final Ref _ref;
  Dio? _dio;

  String get _accessToken =>
      _ref.read(authViewModelProvider).session?.accessToken ?? '';

  Future<void> fetchNotifications({int page = 1, int pageSize = 20}) async {
    if (state.isLoading) return;

    // Skip if no access token
    if (_accessToken.isEmpty) {
      debugPrint('[NotificationNotifier] No access token, skipping fetchNotifications');
      state = state.copyWith(isLoading: false, error: 'Not authenticated');
      return;
    }

    state = state.copyWith(isLoading: true, error: null);

    try {
      _dio ??= Dio(BaseOptions(
        baseUrl: ApiConstants.baseUrl,
        connectTimeout: ApiConstants.timeout,
        receiveTimeout: ApiConstants.timeout,
      ));

      debugPrint('[NotificationNotifier] Fetching notifications from ${ApiConstants.notifications}');

      final response = await _dio!.get(
        ApiConstants.notifications,
        queryParameters: {'page': page, 'pageSize': pageSize},
        options: Options(
          headers: {'Authorization': 'Bearer $_accessToken'},
        ),
      );

      debugPrint('[NotificationNotifier] Response status: ${response.statusCode}');
      debugPrint('[NotificationNotifier] Response data: ${response.data}');

      if (response.statusCode == 200) {
        final data = response.data['data'];
        final notifications = (data['notifications'] as List)
            .map((e) => NotificationDto.fromJson(e))
            .toList();

        debugPrint('[NotificationNotifier] Parsed ${notifications.length} notifications');

        state = state.copyWith(
          notifications: notifications,
          unreadCount: data['unreadCount'] as int,
          isLoading: false,
        );
      } else {
        debugPrint('[NotificationNotifier] Failed with status: ${response.statusCode}');
        state = state.copyWith(
          isLoading: false,
          error: 'Failed to fetch notifications',
        );
      }
    } catch (e, stackTrace) {
      debugPrint('[NotificationNotifier] Error: $e');
      debugPrint('[NotificationNotifier] Stack trace: $stackTrace');
      state = state.copyWith(
        isLoading: false,
        error: e.toString(),
      );
    }
  }

  Future<void> fetchUnreadCount() async {
    // Skip if no access token
    if (_accessToken.isEmpty) {
      debugPrint('[NotificationNotifier] No access token, skipping fetchUnreadCount');
      return;
    }

    try {
      _dio ??= Dio(BaseOptions(
        baseUrl: ApiConstants.baseUrl,
        connectTimeout: ApiConstants.timeout,
        receiveTimeout: ApiConstants.timeout,
      ));

      debugPrint('[NotificationNotifier] Fetching unread count from ${ApiConstants.notificationUnreadCount}');

      final response = await _dio!.get(
        ApiConstants.notificationUnreadCount,
        options: Options(
          headers: {'Authorization': 'Bearer $_accessToken'},
        ),
      );

      debugPrint('[NotificationNotifier] Response status: ${response.statusCode}');
      debugPrint('[NotificationNotifier] Response data: ${response.data}');

      if (response.statusCode == 200) {
        final count = response.data['data']['count'] as int;
        state = state.copyWith(unreadCount: count);
        debugPrint('[NotificationNotifier] Updated unreadCount to: $count');
      }
    } catch (e, stackTrace) {
      debugPrint('[NotificationNotifier] Failed to fetch unread count: $e');
      debugPrint('[NotificationNotifier] Stack trace: $stackTrace');
      // Silently fail
    }
  }

  Future<void> markAsRead(String logId) async {
    try {
      _dio ??= Dio(BaseOptions(
        baseUrl: ApiConstants.baseUrl,
        connectTimeout: ApiConstants.timeout,
        receiveTimeout: ApiConstants.timeout,
      ));

      await _dio!.put(
        '${ApiConstants.notifications}/$logId/read',
        options: Options(
          headers: {'Authorization': 'Bearer $_accessToken'},
        ),
      );

      final updated = state.notifications.map((n) {
        if (n.logId == logId && !n.isRead) {
          return n.copyWith(
            readAt: DateTime.now(),
            isRead: true,
          );
        }
        return n;
      }).toList();

      state = state.copyWith(
        notifications: updated,
        unreadCount: state.unreadCount > 0 ? state.unreadCount - 1 : 0,
      );
    } catch (e) {
      // Re-fetch on error
      fetchUnreadCount();
    }
  }

  Future<void> markAllAsRead() async {
    try {
      _dio ??= Dio(BaseOptions(
        baseUrl: ApiConstants.baseUrl,
        connectTimeout: ApiConstants.timeout,
        receiveTimeout: ApiConstants.timeout,
      ));

      await _dio!.put(
        '${ApiConstants.notifications}/read-all',
        options: Options(
          headers: {'Authorization': 'Bearer $_accessToken'},
        ),
      );

      // Update all notifications to read
      final updated = state.notifications.map((n) {
        if (!n.isRead) {
          return n.copyWith(
            readAt: DateTime.now(),
            isRead: true,
          );
        }
        return n;
      }).toList();

      state = state.copyWith(
        notifications: updated,
        unreadCount: 0,
      );
    } catch (e) {
      // Re-fetch on error
      fetchNotifications();
    }
  }

  void reset() {
    state = const NotificationState();
  }
}

/// Provider
final notificationsProvider =
    StateNotifierProvider<NotificationNotifier, NotificationState>((ref) {
  return NotificationNotifier(ref);
});

/// Provider for unread count (for badge)
final unreadNotificationCountProvider = Provider<int>((ref) {
  final count = ref.watch(notificationsProvider).unreadCount;
  debugPrint('[NotificationNotifier] unreadNotificationCountProvider: $count');
  return count;
});

/// Extension
extension NotificationStateCopyWith on NotificationState {
  NotificationState copyWith({
    List<NotificationDto>? notifications,
    int? unreadCount,
    bool? isLoading,
    String? error,
  }) {
    return NotificationState(
      notifications: notifications ?? this.notifications,
      unreadCount: unreadCount ?? this.unreadCount,
      isLoading: isLoading ?? this.isLoading,
      error: error,
    );
  }
}
