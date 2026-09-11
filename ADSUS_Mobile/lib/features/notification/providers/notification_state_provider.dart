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
  appointmentCancelledByPatient,
  appointmentRescheduled,
  appointmentCheckin,
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
      case NotificationTypeEnum.appointmentCancelledByPatient:
        return 'appointment_cancelled_by_patient';
      case NotificationTypeEnum.appointmentRescheduled:
        return 'appointment_rescheduled';
      case NotificationTypeEnum.appointmentCheckin:
        return 'appointment_checkin';
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
      case NotificationTypeEnum.appointmentCancelledByPatient:
        return 'Lịch hẹn bị hủy';
      case NotificationTypeEnum.appointmentRescheduled:
        return 'Lịch hẹn bị đổi';
      case NotificationTypeEnum.appointmentCheckin:
        return 'Check-in lịch hẹn';
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
      case NotificationTypeEnum.appointmentCancelledByPatient:
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
    this.hasMore = true,
    this.currentPage = 1,
    this.fromDate,
    this.error,
  });

  final List<NotificationDto> notifications;
  final int unreadCount;
  final bool isLoading;
  final bool hasMore;
  final int currentPage;
  final DateTime? fromDate;
  final String? error;
}

/// Notifier for notifications
class NotificationNotifier extends StateNotifier<NotificationState> {
  NotificationNotifier(this._ref) : super(const NotificationState());

  final Ref _ref;
  Dio? _dio;

  // Store pending delete notification (not yet sent to API)
  NotificationDto? _pendingDeleteNotification;

  String get _accessToken =>
      _ref.read(authViewModelProvider).session?.accessToken ?? '';

  Future<void> fetchNotifications({
    int page = 1,
    int pageSize = 20,
    DateTime? fromDate,
    bool isLoadMore = false,
  }) async {
    if (state.isLoading) return;
    if (isLoadMore && !state.hasMore) return;

    // Skip if no access token
    if (_accessToken.isEmpty) {
      debugPrint('[NotificationNotifier] No access token, skipping fetchNotifications');
      state = state.copyWith(isLoading: false, error: 'Not authenticated');
      return;
    }

    // Default fromDate = 60 days ago if not specified
    final effectiveFromDate = fromDate ?? DateTime.now().subtract(const Duration(days: 60));

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
        queryParameters: {
          'page': page,
          'pageSize': pageSize,
          'fromDate': effectiveFromDate.toIso8601String(),
        },
        options: Options(
          headers: {'Authorization': 'Bearer $_accessToken'},
        ),
      );

      debugPrint('[NotificationNotifier] Response status: ${response.statusCode}');
      debugPrint('[NotificationNotifier] Response data: ${response.data}');

      if (response.statusCode == 200) {
        final data = response.data['data'];
        final newNotifications = (data['notifications'] as List)
            .map((e) => NotificationDto.fromJson(e))
            .toList();
        final hasMore = data['hasMore'] as bool? ?? false;

        debugPrint('[NotificationNotifier] Parsed ${newNotifications.length} notifications, hasMore: $hasMore');

        state = state.copyWith(
          notifications: isLoadMore
              ? [...state.notifications, ...newNotifications]
              : newNotifications,
          unreadCount: data['unreadCount'] as int,
          hasMore: hasMore,
          currentPage: page,
          fromDate: effectiveFromDate,
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

  Future<void> loadMoreNotifications() async {
    if (!state.hasMore || state.isLoading) return;
    await fetchNotifications(
      page: state.currentPage + 1,
      pageSize: 10,
      fromDate: state.fromDate,
      isLoadMore: true,
    );
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

  Future<void> deleteNotification(String logId) async {
    // Store notification for undo BEFORE removing
    final notification = state.notifications.firstWhere(
      (n) => n.logId == logId,
      orElse: () => throw Exception('Notification not found'),
    );

    _pendingDeleteNotification = notification;

    // Remove from UI immediately
    final updated = state.notifications
        .where((n) => n.logId != logId)
        .toList();

    final deletedWasUnread = !notification.isRead;
    state = state.copyWith(
      notifications: updated,
      unreadCount: deletedWasUnread && state.unreadCount > 0
          ? state.unreadCount - 1
          : state.unreadCount,
    );

    // Call API to delete immediately
    try {
      _dio ??= Dio(BaseOptions(
        baseUrl: ApiConstants.baseUrl,
        connectTimeout: ApiConstants.timeout,
        receiveTimeout: ApiConstants.timeout,
      ));

      await _dio!.delete(
        '${ApiConstants.notifications}/$logId',
        options: Options(
          headers: {'Authorization': 'Bearer $_accessToken'},
        ),
      );
      debugPrint('[NotificationNotifier] Deleted notification from API: $logId');
    } catch (e) {
      debugPrint('[NotificationNotifier] Failed to delete notification: $e');
      // Re-fetch to sync on error
      fetchNotifications();
    }
  }

  /// Undo last delete operation - restore to correct position based on SentAt + call API
  Future<void> undoDelete() async {
    if (_pendingDeleteNotification == null) return;

    final notification = _pendingDeleteNotification!;
    _pendingDeleteNotification = null;

    // Find correct insert position based on SentAt (descending order - newest first)
    final sentAt = notification.sentAt;
    int insertIndex = state.notifications.length;

    for (int i = 0; i < state.notifications.length; i++) {
      if (state.notifications[i].sentAt.isBefore(sentAt)) {
        insertIndex = i;
        break;
      }
    }

    // Insert at correct position (local first)
    final updated = [...state.notifications];
    updated.insert(insertIndex, notification);

    final restoredWasUnread = !notification.isRead;
    state = state.copyWith(
      notifications: updated,
      unreadCount: restoredWasUnread
          ? state.unreadCount + 1
          : state.unreadCount,
    );

    // Call API to restore on backend
    try {
      _dio ??= Dio(BaseOptions(
        baseUrl: ApiConstants.baseUrl,
        connectTimeout: ApiConstants.timeout,
        receiveTimeout: ApiConstants.timeout,
      ));

      await _dio!.put(
        '${ApiConstants.notifications}/${notification.logId}/restore',
        options: Options(
          headers: {'Authorization': 'Bearer $_accessToken'},
        ),
      );
      debugPrint('[NotificationNotifier] Restored notification via API: ${notification.logId}');
    } catch (e) {
      debugPrint('[NotificationNotifier] Failed to restore: $e');
    }

    // Re-fetch to sync with server
    await fetchNotifications();
  }

  /// Mark notification as unread (CALL API)
  Future<void> markAsUnread(String logId) async {
    // Call API first
    try {
      _dio ??= Dio(BaseOptions(
        baseUrl: ApiConstants.baseUrl,
        connectTimeout: ApiConstants.timeout,
        receiveTimeout: ApiConstants.timeout,
      ));

      await _dio!.put(
        '${ApiConstants.notifications}/$logId/unread',
        options: Options(
          headers: {'Authorization': 'Bearer $_accessToken'},
        ),
      );
      debugPrint('[NotificationNotifier] Marked as unread via API: $logId');
    } catch (e) {
      debugPrint('[NotificationNotifier] Failed to mark as unread: $e');
    }

    // Update local state
    final updated = state.notifications.map((n) {
      if (n.logId == logId && n.isRead) {
        return n.copyWith(readAt: null, isRead: false);
      }
      return n;
    }).toList();

    state = state.copyWith(
      notifications: updated,
      unreadCount: state.unreadCount + 1,
    );
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
    bool? hasMore,
    int? currentPage,
    DateTime? fromDate,
    String? error,
  }) {
    return NotificationState(
      notifications: notifications ?? this.notifications,
      unreadCount: unreadCount ?? this.unreadCount,
      isLoading: isLoading ?? this.isLoading,
      hasMore: hasMore ?? this.hasMore,
      currentPage: currentPage ?? this.currentPage,
      fromDate: fromDate ?? this.fromDate,
      error: error,
    );
  }
}
