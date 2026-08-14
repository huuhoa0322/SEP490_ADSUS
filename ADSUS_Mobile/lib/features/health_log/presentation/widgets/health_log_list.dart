import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_theme.dart';
import '../viewmodels/health_log_view_model.dart';
import 'health_log_card.dart';

/// Danh sách nhật ký sức khỏe theo ngày đã chọn (Module 9 - FT-35).
///
/// Quan sát [healthLogsProvider]:
///   - [AsyncLoading] → hiển thị loading spinner
///   - [AsyncError]  → hiển thị thông báo lỗi + nút retry
///   - []           → hiển thị trạng thái rỗng
///   - [List]       → hiển thị ListView các [HealthLogCard]
class HealthLogList extends ConsumerWidget {
  const HealthLogList({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final logsAsync = ref.watch(healthLogsProvider);

    return logsAsync.when(
      loading: () => const Center(
        child: Padding(
          padding: EdgeInsets.all(32),
          child: CircularProgressIndicator(
            color: AppColors.teal,
          ),
        ),
      ),

      error: (error, stackTrace) => _ErrorState(
        message: error.toString(),
        onRetry: () => ref.invalidate(healthLogsProvider),
      ),

      data: (logs) {
        if (logs.isEmpty) {
          return const _EmptyState();
        }

        return ListView.separated(
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          itemCount: logs.length,
          separatorBuilder: (_, index) => const SizedBox(height: 12),
          itemBuilder: (context, index) => HealthLogCard(
            healthLog: logs[index],
          ),
        );
      },
    );
  }
}

/// Trạng thái rỗng — không có nhật ký nào.
class _EmptyState extends StatelessWidget {
  const _EmptyState();

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(
              Icons.note_add_outlined,
              size: 56,
              color: AppColors.muted.withValues(alpha: 0.5),
            ),
            const SizedBox(height: 16),
            Text(
              'Chưa có nhật ký nào',
              style: TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.w600,
                color: AppColors.muted,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              'Nhấn nút + để tạo nhật ký mới',
              style: TextStyle(
                fontSize: 13,
                color: AppColors.muted.withValues(alpha: 0.7),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Trạng thái lỗi — có nút retry.
class _ErrorState extends StatelessWidget {
  const _ErrorState({
    required this.message,
    required this.onRetry,
  });

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(
              Icons.error_outline,
              size: 56,
              color: AppColors.danger.withValues(alpha: 0.7),
            ),
            const SizedBox(height: 16),
            Text(
              'Đã xảy ra lỗi',
              style: TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.w600,
                color: AppColors.navy,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              message,
              textAlign: TextAlign.center,
              style: TextStyle(
                fontSize: 13,
                color: AppColors.muted,
              ),
            ),
            const SizedBox(height: 20),
            OutlinedButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh, size: 18),
              label: const Text('Thử lại'),
              style: OutlinedButton.styleFrom(
                foregroundColor: AppColors.teal,
                side: const BorderSide(color: AppColors.teal),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
