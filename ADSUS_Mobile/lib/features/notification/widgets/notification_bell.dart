import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/services/notification_navigation_service.dart';
import '../../../core/theme/app_theme.dart';
import '../providers/notification_state_provider.dart';
import '../screens/notification_history_screen.dart';

/// Notification bell icon with unread badge and dropdown list.
/// Shows on Home screen AppBar.
class NotificationBell extends ConsumerStatefulWidget {
  const NotificationBell({super.key});

  @override
  ConsumerState<NotificationBell> createState() => _NotificationBellState();
}

class _NotificationBellState extends ConsumerState<NotificationBell> {
  final LayerLink _layerLink = LayerLink();
  OverlayEntry? _overlayEntry;
  bool _isOpen = false;

  @override
  void initState() {
    super.initState();
    debugPrint('[NotificationBell] initState called');
    // Fetch initial unread count
    WidgetsBinding.instance.addPostFrameCallback((_) {
      debugPrint('[NotificationBell] PostFrameCallback - fetching unread count');
      ref.read(notificationsProvider.notifier).fetchUnreadCount();
    });
  }

  void _toggleDropdown() {
    if (_isOpen) {
      _closeDropdown();
    } else {
      _openDropdown();
    }
  }

  void _openDropdown() {
    // Use showModalBottomSheet instead of Overlay for better centering
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (context) => DraggableScrollableSheet(
        initialChildSize: 0.6,
        minChildSize: 0.3,
        maxChildSize: 0.9,
        builder: (_, scrollController) => Container(
          decoration: const BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
          ),
          child: Column(
            children: [
              // Drag handle
              Container(
                margin: const EdgeInsets.only(top: 12),
                width: 40,
                height: 4,
                decoration: BoxDecoration(
                  color: AppColors.border,
                  borderRadius: BorderRadius.circular(2),
                ),
              ),
              // Header
              Padding(
                padding: const EdgeInsets.all(16),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    const Text(
                      'Thông báo',
                      style: TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.bold,
                        color: AppColors.navy,
                      ),
                    ),
                    IconButton(
                      icon: const Icon(Icons.close),
                      onPressed: () => Navigator.of(context).pop(),
                    ),
                  ],
                ),
              ),
              const Divider(height: 1),
              // Content
              Expanded(
                child: _NotificationDropdownContent(
                  onClose: () => Navigator.of(context).pop(),
                  scrollController: scrollController,
                ),
              ),
            ],
          ),
        ),
      ),
    ).then((_) {
      setState(() {
        _isOpen = false;
      });
    });

    setState(() {
      _isOpen = true;
    });

    // Fetch notifications when opening dropdown
    ref.read(notificationsProvider.notifier).fetchNotifications();
  }

  void _closeDropdown() {
    Navigator.of(context).pop();
    setState(() {
      _isOpen = false;
    });
  }

  @override
  void dispose() {
    _overlayEntry?.remove();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final unreadCount = ref.watch(unreadNotificationCountProvider);
    debugPrint('[NotificationBell] build() called, unreadCount: $unreadCount');

    return CompositedTransformTarget(
      link: _layerLink,
      child: Stack(
        children: [
          IconButton(
            icon: Icon(
              _isOpen ? Icons.notifications : Icons.notifications_outlined,
            ),
            color: AppColors.navy,
            onPressed: _toggleDropdown,
          ),
          if (unreadCount > 0)
            Positioned(
              right: 8,
              top: 8,
              child: GestureDetector(
                onTap: _toggleDropdown,
                child: Container(
                  padding: const EdgeInsets.all(4),
                  decoration: const BoxDecoration(
                    color: Colors.red,
                    shape: BoxShape.circle,
                  ),
                  constraints: const BoxConstraints(
                    minWidth: 18,
                    minHeight: 18,
                  ),
                  child: Text(
                    unreadCount > 99 ? '99+' : unreadCount.toString(),
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 10,
                      fontWeight: FontWeight.bold,
                    ),
                    textAlign: TextAlign.center,
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }
}

class _NotificationDropdownContent extends ConsumerWidget {
  const _NotificationDropdownContent({
    required this.onClose,
    this.scrollController,
  });

  final VoidCallback onClose;
  final ScrollController? scrollController;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(notificationsProvider);
    final notifications = state.notifications;

    return Column(
      children: [
        // Content
        Expanded(
          child: state.isLoading
              ? const Center(child: CircularProgressIndicator())
              : notifications.isEmpty
                  ? Center(
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          Icon(
                            Icons.notifications_none_outlined,
                            size: 48,
                            color: AppColors.muted,
                          ),
                          const SizedBox(height: 8),
                          Text(
                            'Không có thông báo nào',
                            style: TextStyle(
                              color: AppColors.muted,
                              fontSize: 14,
                            ),
                          ),
                        ],
                      ),
                    )
                  : ListView.separated(
                      controller: scrollController,
                      padding: EdgeInsets.zero,
                      itemCount: notifications.length,
                      separatorBuilder: (context, index) => const Divider(height: 1),
                      itemBuilder: (context, index) {
                        final notification = notifications[index];
                        return _NotificationItem(
                          notification: notification,
                          onTap: () {
                            // Mark as read and close dropdown
                            ref.read(notificationsProvider.notifier).markAsRead(notification.logId);
                            onClose();
                            // Navigate to appropriate screen based on notification type
                            _showNotificationDetail(context, notification, ref);
                          },
                        );
                      },
                    ),
        ),

        // Footer with "View all" and "Mark all as read" (only show if there are notifications)
        if (notifications.isNotEmpty)
          Container(
            padding: const EdgeInsets.all(12),
            decoration: const BoxDecoration(
              border: Border(
                top: BorderSide(color: AppColors.border),
              ),
            ),
            child: Column(
              children: [
                // Mark all as read button
                SizedBox(
                  width: double.infinity,
                  child: TextButton.icon(
                    onPressed: () {
                      ref.read(notificationsProvider.notifier).markAllAsRead();
                    },
                    icon: const Icon(Icons.done_all, size: 18),
                    label: const Text('Đánh dấu tất cả đã đọc'),
                    style: TextButton.styleFrom(
                      foregroundColor: AppColors.teal,
                    ),
                  ),
                ),
                const SizedBox(height: 4),
                // View all button
                SizedBox(
                  width: double.infinity,
                  child: TextButton(
                    onPressed: () {
                      onClose();
                      Navigator.of(context).push(
                        MaterialPageRoute<void>(
                          builder: (_) => const NotificationHistoryScreen(),
                        ),
                      );
                    },
                    child: const Text('Xem tất cả thông báo'),
                  ),
                ),
              ],
            ),
          ),
      ],
    );
  }

  void _showNotificationDetail(BuildContext context, NotificationDto notification, WidgetRef ref) {
    // Navigate based on notification type
    final navService = NotificationNavigationService(context, ref);
    navService.navigate(notification);
  }
}

class _NotificationItem extends StatelessWidget {
  const _NotificationItem({
    required this.notification,
    required this.onTap,
  });

  final NotificationDto notification;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        color: notification.isRead ? Colors.white : AppColors.unreadBg,
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Unread indicator
            if (!notification.isRead)
              Container(
                margin: const EdgeInsets.only(top: 6, right: 8),
                width: 8,
                height: 8,
                decoration: const BoxDecoration(
                  color: Colors.red,
                  shape: BoxShape.circle,
                ),
              )
            else
              const SizedBox(width: 16),

            // Icon based on type
            Container(
              margin: const EdgeInsets.only(right: 12),
              child: Icon(
                _getIconForType(notification.typeEnum),
                color: _getColorForType(notification.typeEnum),
                size: 24,
              ),
            ),

            // Content
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    notification.title,
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight:
                          notification.isRead ? FontWeight.normal : FontWeight.w600,
                      color: AppColors.navy,
                    ),
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                  ),
                  if (notification.body != null) ...[
                    const SizedBox(height: 4),
                    Text(
                      notification.body!,
                      style: const TextStyle(
                        fontSize: 12,
                        color: AppColors.muted,
                      ),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ],
                  const SizedBox(height: 4),
                  Text(
                    _formatTime(notification.sentAt),
                    style: const TextStyle(
                      fontSize: 11,
                      color: AppColors.muted,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  IconData _getIconForType(NotificationTypeEnum type) {
    switch (type) {
      case NotificationTypeEnum.medicationReminder:
      case NotificationTypeEnum.medicationConfirmation:
        return Icons.medication_outlined;
      case NotificationTypeEnum.appointmentReminder:
      case NotificationTypeEnum.appointmentBooking:
      case NotificationTypeEnum.appointmentCancellation:
        return Icons.calendar_today_outlined;
      case NotificationTypeEnum.medicalRecordAdded:
        return Icons.folder_outlined;
      case NotificationTypeEnum.blogNewPost:
        return Icons.article_outlined;
      case NotificationTypeEnum.weeklyHealthReport:
      case NotificationTypeEnum.adherenceSummary:
        return Icons.assessment_outlined;
      case NotificationTypeEnum.healthlogReminder:
        return Icons.note_alt_outlined;
      default:
        return Icons.notifications_outlined;
    }
  }

  Color _getColorForType(NotificationTypeEnum type) {
    switch (type) {
      case NotificationTypeEnum.medicationReminder:
      case NotificationTypeEnum.medicationConfirmation:
        return Colors.red;
      case NotificationTypeEnum.appointmentReminder:
      case NotificationTypeEnum.appointmentBooking:
      case NotificationTypeEnum.appointmentCancellation:
        return Colors.blue;
      case NotificationTypeEnum.medicalRecordAdded:
        return Colors.purple;
      case NotificationTypeEnum.blogNewPost:
      case NotificationTypeEnum.weeklyHealthReport:
      case NotificationTypeEnum.adherenceSummary:
        return Colors.green;
      default:
        return AppColors.muted;
    }
  }

  String _formatTime(DateTime dateTime) {
    final now = DateTime.now();
    final difference = now.difference(dateTime);

    if (difference.inMinutes < 1) {
      return 'Vừa xong';
    } else if (difference.inMinutes < 60) {
      return '${difference.inMinutes} phút trước';
    } else if (difference.inHours < 24) {
      return '${difference.inHours} giờ trước';
    } else if (difference.inDays < 7) {
      return '${difference.inDays} ngày trước';
    } else {
      return '${dateTime.day}/${dateTime.month}/${dateTime.year}';
    }
  }
}
