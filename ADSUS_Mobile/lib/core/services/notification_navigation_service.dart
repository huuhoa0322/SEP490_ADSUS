import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../features/auth/presentation/views/home_screen.dart';
import '../../features/appointment_scheduling/presentation/views/my_appointments_screen.dart';
import '../../features/engagement/presentation/views/blog_detail_screen.dart';
import '../../features/engagement/presentation/views/blog_list_screen.dart';
import '../../features/health_log/presentation/views/health_log_screen.dart';
import '../../features/medical_record/presentation/views/medical_record_detail_screen.dart';
import '../../features/notification/providers/notification_state_provider.dart';
import '../theme/app_theme.dart';

/// Service xử lý navigation dựa trên notification type
class NotificationNavigationService {
  final BuildContext context;
  final WidgetRef ref;

  NotificationNavigationService(this.context, this.ref);

  /// Navigate đến màn hình phù hợp với notification
  Future<void> navigate(NotificationDto notification) async {
    // Mark as read first
    if (!notification.isRead) {
      debugPrint('[NotificationNavigationService] Marking notification ${notification.logId} as read');
      ref.read(notificationsProvider.notifier).markAsRead(notification.logId);
    }

    switch (notification.typeEnum) {
      case NotificationTypeEnum.medicationReminder:
      case NotificationTypeEnum.medicationConfirmation:
        _navigateToMedication(notification);
        break;

      case NotificationTypeEnum.appointmentReminder:
      case NotificationTypeEnum.appointmentBooking:
      case NotificationTypeEnum.appointmentCancellation:
        _navigateToAppointment(notification);
        break;

      case NotificationTypeEnum.medicalRecordAdded:
        _navigateToMedicalRecord(notification);
        break;

      case NotificationTypeEnum.blogNewPost:
        _navigateToBlogPost(notification);
        break;

      case NotificationTypeEnum.weeklyHealthReport:
      case NotificationTypeEnum.adherenceSummary:
      case NotificationTypeEnum.healthlogReminder:
        _navigateToHealthLog(notification);
        break;

      case NotificationTypeEnum.general:
        _navigateToHome();
        break;
    }
  }

  void _navigateToMedication(NotificationDto notification) {
    // Navigate to home - medication tab will show pending intakes
    // relatedId is scheduleId, can be used later for deep linking
    Navigator.of(context).push(
      MaterialPageRoute<void>(builder: (_) => const HomeScreen()),
    );
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(notification.title),
        backgroundColor: AppColors.teal,
        duration: const Duration(seconds: 3),
      ),
    );
  }

  void _navigateToAppointment(NotificationDto notification) {
    final appointmentId = notification.relatedId;

    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => MyAppointmentsScreen(
          highlightAppointmentId: appointmentId,
        ),
      ),
    );
  }

  void _navigateToMedicalRecord(NotificationDto notification) {
    // relatedId is recordId (caseId in the app)
    final caseId = notification.relatedId;
    if (caseId != null) {
      Navigator.of(context).push(
        MaterialPageRoute<void>(
          builder: (_) => MedicalRecordDetailScreen(caseId: caseId),
        ),
      );
    } else {
      _navigateToHome();
    }
  }

  void _navigateToBlogPost(NotificationDto notification) {
    final postId = notification.relatedId;
    if (postId != null) {
      Navigator.of(context).push(
        MaterialPageRoute<void>(
          builder: (_) => BlogDetailScreen(postId: postId),
        ),
      );
    } else {
      Navigator.of(context).push(
        MaterialPageRoute<void>(
          builder: (_) => const BlogListScreen(),
        ),
      );
    }
  }

  void _navigateToHealthLog(NotificationDto notification) {
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => const HealthLogScreen(),
      ),
    );
  }

  void _navigateToHome() {
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute<void>(builder: (_) => const HomeScreen()),
      (route) => false,
    );
  }
}
