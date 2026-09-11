import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/services/notification_navigation_service.dart';
import '../../../core/theme/app_theme.dart';
import '../providers/notification_state_provider.dart';

/// Screen to display notification history.
class NotificationHistoryScreen extends ConsumerStatefulWidget {
  const NotificationHistoryScreen({super.key});

  @override
  ConsumerState<NotificationHistoryScreen> createState() =>
      _NotificationHistoryScreenState();
}

class _NotificationHistoryScreenState
    extends ConsumerState<NotificationHistoryScreen> {
  final ScrollController _scrollController = ScrollController();

  /// Delete notification with undo snackbar
  void _deleteWithUndo(NotificationDto notification) {
    final notifier = ref.read(notificationsProvider.notifier);

    // Remove from UI and call API immediately
    notifier.deleteNotification(notification.logId);

    // Show snackbar with undo (API already called)
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: const Text('Thông báo đã bị xóa'),
        behavior: SnackBarBehavior.floating,
        duration: const Duration(seconds: 5),
        action: SnackBarAction(
          label: 'Hoàn tác',
          onPressed: () {
            notifier.undoDelete();
          },
        ),
      ),
    );
  }

  @override
  void initState() {
    super.initState();
    // Load notifications trong 60 ngày với 20 items đầu tiên
    Future.microtask(() {
      ref.read(notificationsProvider.notifier).fetchNotifications(
            fromDate: DateTime.now().subtract(const Duration(days: 60)),
            pageSize: 20,
          );
    });

    // Lắng nghe scroll để load thêm
    _scrollController.addListener(_onScroll);
  }

  @override
  void dispose() {
    _scrollController.removeListener(_onScroll);
    _scrollController.dispose();
    super.dispose();
  }

  void _onScroll() {
    if (_scrollController.position.pixels >=
        _scrollController.position.maxScrollExtent - 200) {
      // Khi còn cách bottom 200px, load thêm
      ref.read(notificationsProvider.notifier).loadMoreNotifications();
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(notificationsProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Thông báo'),
        backgroundColor: AppColors.teal,
        foregroundColor: Colors.white,
      ),
      body: _buildBody(state),
    );
  }

  Widget _buildBody(NotificationState state) {
    if (state.isLoading && state.notifications.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }

    if (state.error != null && state.notifications.isEmpty) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.error_outline, size: 64, color: AppColors.danger),
            const SizedBox(height: 16),
            Text(state.error!, style: const TextStyle(color: AppColors.danger)),
            const SizedBox(height: 16),
            ElevatedButton(
              onPressed: () => ref.read(notificationsProvider.notifier).fetchNotifications(
                    fromDate: DateTime.now().subtract(const Duration(days: 60)),
                    pageSize: 20,
                  ),
              child: const Text('Thử lại'),
            ),
          ],
        ),
      );
    }

    if (state.notifications.isEmpty) {
      return const Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.notifications_none, size: 64, color: AppColors.muted),
            SizedBox(height: 16),
            Text('Không có thông báo nào',
                style: TextStyle(color: AppColors.muted)),
          ],
        ),
      );
    }

    return RefreshIndicator(
      onRefresh: () => ref.read(notificationsProvider.notifier).fetchNotifications(
            fromDate: DateTime.now().subtract(const Duration(days: 60)),
            pageSize: 20,
          ),
      child: ListView.builder(
        controller: _scrollController,
        padding: const EdgeInsets.all(16),
        itemCount: state.notifications.length + (state.hasMore ? 1 : 0),
        itemBuilder: (context, index) {
          // Nếu là item cuối và còn hasMore, hiện loading indicator
          if (index == state.notifications.length && state.hasMore) {
            return const Center(
              child: Padding(
                padding: EdgeInsets.all(16),
                child: CircularProgressIndicator(),
              ),
            );
          }

          final notification = state.notifications[index];
          return Dismissible(
            key: Key(notification.logId),
            direction: DismissDirection.endToStart,
            background: Container(
              alignment: Alignment.centerRight,
              padding: const EdgeInsets.only(right: 20),
              color: Colors.red,
              child: const Icon(Icons.delete, color: Colors.white),
            ),
            onDismissed: (_) {
              _deleteWithUndo(notification);
            },
            child: Card(
              margin: const EdgeInsets.only(bottom: 8),
              child: ListTile(
                leading: Icon(
                  notification.isRead
                      ? Icons.notifications_outlined
                      : Icons.notifications,
                  color: notification.isRead ? AppColors.muted : AppColors.teal,
                ),
                title: Text(
                  notification.title,
                  style: TextStyle(
                    fontWeight:
                        notification.isRead ? FontWeight.normal : FontWeight.bold,
                  ),
                ),
                subtitle: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    if (notification.body != null) Text(notification.body!),
                    Text(
                      _formatDate(notification.sentAt),
                      style: const TextStyle(fontSize: 12, color: AppColors.muted),
                    ),
                  ],
                ),
                trailing: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    if (!notification.isRead)
                      Container(
                        width: 8,
                        height: 8,
                        margin: const EdgeInsets.only(right: 4),
                        decoration: const BoxDecoration(
                          color: AppColors.teal,
                          shape: BoxShape.circle,
                        ),
                      ),
                    PopupMenuButton<String>(
                      icon: const Icon(Icons.more_vert, size: 20),
                      padding: EdgeInsets.zero,
                      onSelected: (value) {
                        switch (value) {
                          case 'mark_read':
                            ref.read(notificationsProvider.notifier)
                                .markAsRead(notification.logId);
                            break;
                          case 'mark_unread':
                            ref.read(notificationsProvider.notifier)
                                .markAsUnread(notification.logId);
                            break;
                          case 'delete':
                            _deleteWithUndo(notification);
                            break;
                        }
                      },
                      itemBuilder: (context) => [
                        PopupMenuItem(
                          value: notification.isRead ? 'mark_unread' : 'mark_read',
                          child: Row(
                            children: [
                              Icon(
                                notification.isRead
                                    ? Icons.mark_email_unread
                                    : Icons.mark_email_read,
                                size: 20,
                              ),
                              const SizedBox(width: 12),
                              Text(
                                notification.isRead
                                    ? 'Đánh dấu chưa đọc'
                                    : 'Đánh dấu đã đọc',
                              ),
                            ],
                          ),
                        ),
                        const PopupMenuDivider(),
                        const PopupMenuItem(
                          value: 'delete',
                          child: Row(
                            children: [
                              Icon(Icons.delete, color: Colors.red, size: 20),
                              SizedBox(width: 12),
                              Text(
                                'Xóa thông báo',
                                style: TextStyle(color: Colors.red),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
                onTap: () {
                  final navService = NotificationNavigationService(context, ref);
                  navService.navigate(notification);
                },
              ),
            ),
          );
        },
      ),
    );
  }

  String _formatDate(DateTime date) {
    final now = DateTime.now();
    final diff = now.difference(date);

    if (diff.inMinutes < 60) {
      return '${diff.inMinutes} phút trước';
    } else if (diff.inHours < 24) {
      return '${diff.inHours} giờ trước';
    } else if (diff.inDays < 7) {
      return '${diff.inDays} ngày trước';
    } else {
      return '${date.day}/${date.month}/${date.year}';
    }
  }
}
