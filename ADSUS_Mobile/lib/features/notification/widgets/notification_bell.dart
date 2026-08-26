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
    final overlay = Overlay.of(context);

    _overlayEntry = _createOverlayEntry();
    overlay.insert(_overlayEntry!);
    setState(() {
      _isOpen = true;
    });

    // Fetch notifications when opening dropdown
    ref.read(notificationsProvider.notifier).fetchNotifications();
  }

  void _closeDropdown() {
    _overlayEntry?.remove();
    _overlayEntry = null;
    setState(() {
      _isOpen = false;
    });
  }

  OverlayEntry _createOverlayEntry() {
    RenderBox renderBox = context.findRenderObject() as RenderBox;
    var size = renderBox.size;

    return OverlayEntry(
      builder: (context) => Positioned(
        width: 320,
        child: CompositedTransformFollower(
          link: _layerLink,
          showWhenUnlinked: false,
          offset: Offset(-220, size.height + 8),
          child: Material(
            elevation: 8,
            borderRadius: BorderRadius.circular(12),
            child: Container(
              constraints: const BoxConstraints(maxHeight: 400),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: AppColors.border),
              ),
              child: _NotificationDropdownContent(
                onClose: _closeDropdown,
              ),
            ),
          ),
        ),
      ),
    );
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
  const _NotificationDropdownContent({required this.onClose});

  final VoidCallback onClose;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(notificationsProvider);
    final notifications = state.notifications;

    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        // Header
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
          decoration: const BoxDecoration(
            border: Border(
              bottom: BorderSide(color: AppColors.border),
            ),
          ),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Text(
                'Thông báo',
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.bold,
                  color: AppColors.navy,
                ),
              ),
              IconButton(
                icon: const Icon(Icons.close, size: 20),
                padding: EdgeInsets.zero,
                constraints: const BoxConstraints(),
                onPressed: onClose,
              ),
            ],
          ),
        ),

        // Content
        if (state.isLoading)
          const Padding(
            padding: EdgeInsets.all(32),
            child: CircularProgressIndicator(),
          )
        else if (notifications.isEmpty)
          const Padding(
            padding: EdgeInsets.all(32),
            child: Column(
              children: [
                Icon(
                  Icons.notifications_none_outlined,
                  size: 48,
                  color: AppColors.muted,
                ),
                SizedBox(height: 8),
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
        else
          Flexible(
            child: ListView.separated(
              shrinkWrap: true,
              padding: EdgeInsets.zero,
              itemCount: notifications.length > 5 ? 5 : notifications.length,
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

        // Footer with "View all" and "Mark all as read"
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
