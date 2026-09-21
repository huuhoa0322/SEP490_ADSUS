import 'package:flutter_test/flutter_test.dart';
import 'package:adsus_mobile/features/notification/providers/notification_state_provider.dart';

void main() {
  group('NotificationTypeEnum Tests', () {
    test('fromString parses all 14 notification types correctly', () {
      expect(NotificationTypeEnumExtension.fromString('general'), NotificationTypeEnum.general);
      expect(NotificationTypeEnumExtension.fromString('medication_reminder'), NotificationTypeEnum.medicationReminder);
      expect(NotificationTypeEnumExtension.fromString('medication_confirmation'), NotificationTypeEnum.medicationConfirmation);
      expect(NotificationTypeEnumExtension.fromString('appointment_booking'), NotificationTypeEnum.appointmentBooking);
      expect(NotificationTypeEnumExtension.fromString('appointment_reminder'), NotificationTypeEnum.appointmentReminder);
      expect(NotificationTypeEnumExtension.fromString('appointment_cancellation'), NotificationTypeEnum.appointmentCancellation);
      expect(NotificationTypeEnumExtension.fromString('appointment_cancelled_by_patient'), NotificationTypeEnum.appointmentCancelledByPatient);
      expect(NotificationTypeEnumExtension.fromString('appointment_rescheduled'), NotificationTypeEnum.appointmentRescheduled);
      expect(NotificationTypeEnumExtension.fromString('appointment_checkin'), NotificationTypeEnum.appointmentCheckin);
      expect(NotificationTypeEnumExtension.fromString('healthlog_reminder'), NotificationTypeEnum.healthlogReminder);
      expect(NotificationTypeEnumExtension.fromString('medical_record_added'), NotificationTypeEnum.medicalRecordAdded);
      expect(NotificationTypeEnumExtension.fromString('blog_new_post'), NotificationTypeEnum.blogNewPost);
      expect(NotificationTypeEnumExtension.fromString('weekly_health_report'), NotificationTypeEnum.weeklyHealthReport);
      expect(NotificationTypeEnumExtension.fromString('adherence_summary'), NotificationTypeEnum.adherenceSummary);
      expect(NotificationTypeEnumExtension.fromString('unknown_type'), NotificationTypeEnum.general);
      expect(NotificationTypeEnumExtension.fromString(null), NotificationTypeEnum.general);
    });

    test('displayName returns descriptive Vietnamese labels', () {
      expect(NotificationTypeEnum.medicationReminder.displayName, 'Nhắc uống thuốc');
      expect(NotificationTypeEnum.appointmentReminder.displayName, 'Nhắc lịch khám');
      expect(NotificationTypeEnum.medicalRecordAdded.displayName, 'Hồ sơ y tế mới');
      expect(NotificationTypeEnum.healthlogReminder.displayName, 'Nhắc nhật ký sức khoẻ');
    });
  });

  group('NotificationDto Tests', () {
    test('fromJson parses full payload with metadata', () {
      final json = {
        'logId': 'log-001',
        'type': 'appointment_reminder',
        'title': 'Lịch khám sắp tới',
        'body': 'Bạn có lịch khám lúc 09:00',
        'deepLink': '/appointments/app-123',
        'metadata': {'appointmentId': 'app-123'},
        'sentAt': '2026-09-18T08:00:00.000Z',
        'readAt': null,
        'isRead': false,
      };

      final dto = NotificationDto.fromJson(json);
      expect(dto.logId, 'log-001');
      expect(dto.typeEnum, NotificationTypeEnum.appointmentReminder);
      expect(dto.title, 'Lịch khám sắp tới');
      expect(dto.relatedId, 'app-123');
      expect(dto.isRead, isFalse);
      expect(dto.readAt, isNull);
    });

    test('relatedId extracts scheduleId for medicationReminder', () {
      final dto = NotificationDto(
        logId: '1',
        type: 'medication_reminder',
        title: 'Uống thuốc',
        sentAt: DateTime(2026, 9, 18),
        isRead: false,
        metadata: {'scheduleId': 'sched-999'},
      );

      expect(dto.relatedId, 'sched-999');
    });

    test('relatedId extracts recordId for medicalRecordAdded', () {
      final dto = NotificationDto(
        logId: '2',
        type: 'medical_record_added',
        title: 'Hồ sơ mới',
        sentAt: DateTime(2026, 9, 18),
        isRead: false,
        metadata: {'recordId': 'rec-555'},
      );

      expect(dto.relatedId, 'rec-555');
    });

    test('relatedId returns null when metadata is absent', () {
      final dto = NotificationDto(
        logId: '3',
        type: 'general',
        title: 'Chào mừng',
        sentAt: DateTime(2026, 9, 18),
        isRead: true,
      );

      expect(dto.relatedId, isNull);
    });
  });

  group('NotificationState Invariants', () {
    test('unreadCount decrement policy on mark as read', () {
      final n1 = NotificationDto(logId: '1', type: 'general', title: 'T1', sentAt: DateTime.now(), isRead: false);
      final n2 = NotificationDto(logId: '2', type: 'general', title: 'T2', sentAt: DateTime.now(), isRead: true);

      final state = NotificationState(notifications: [n1, n2], unreadCount: 1);

      // Marking unread as read decreases unreadCount
      final updatedNotifications = state.notifications.map((n) {
        if (n.logId == '1') return n.copyWith(isRead: true, readAt: DateTime.now());
        return n;
      }).toList();

      final newState = state.copyWith(
        notifications: updatedNotifications,
        unreadCount: state.unreadCount > 0 ? state.unreadCount - 1 : 0,
      );

      expect(newState.unreadCount, 0);
      expect(newState.notifications.first.isRead, isTrue);
    });

    test('optimistic delete decreases unreadCount only if deleted item was unread', () {
      final unread = NotificationDto(logId: 'u1', type: 'general', title: 'Unread', sentAt: DateTime.now(), isRead: false);
      final read = NotificationDto(logId: 'r1', type: 'general', title: 'Read', sentAt: DateTime.now(), isRead: true);

      final state = NotificationState(notifications: [unread, read], unreadCount: 1);

      // 1. Delete read item -> unreadCount remains 1
      final stateAfterReadDelete = state.copyWith(
        notifications: [unread],
        unreadCount: !read.isRead && state.unreadCount > 0 ? state.unreadCount - 1 : state.unreadCount,
      );
      expect(stateAfterReadDelete.unreadCount, 1);

      // 2. Delete unread item -> unreadCount drops to 0
      final stateAfterUnreadDelete = stateAfterReadDelete.copyWith(
        notifications: [],
        unreadCount: !unread.isRead && stateAfterReadDelete.unreadCount > 0 ? stateAfterReadDelete.unreadCount - 1 : stateAfterReadDelete.unreadCount,
      );
      expect(stateAfterUnreadDelete.unreadCount, 0);
    });
  });
}
