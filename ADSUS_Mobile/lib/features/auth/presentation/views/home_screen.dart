import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_theme.dart';
import '../../../appointment_scheduling/presentation/views/book_appointment_screen.dart';
import '../../../appointment_scheduling/presentation/views/my_appointments_screen.dart';
import '../../../engagement/presentation/views/blog_list_screen.dart';
import '../../../health_log/presentation/views/health_log_screen.dart';
import '../../../medical_record/presentation/views/medical_record_list_screen.dart';
import '../../../medication_reminder/presentation/viewmodels/intake_view_model.dart';
import '../../../medication_reminder/presentation/widgets/adherence_pill_badge.dart';
import '../../../notification/widgets/notification_bell.dart';
import '../viewmodels/auth_view_model.dart';

/// Màn hình Trang chủ của bệnh nhân sau khi đăng nhập.
///
/// Chứa:
///   - Header: lời chào + tên
///   - Card "Thuốc hôm nay": tổng tuân thủ hôm nay + số liều còn lại
///   - Grid 2 cột × 5 lối tắt: Nhật ký, Đặt lịch khám, Lịch sử khám, Bài viết sức khoẻ, Lịch khám của tôi
class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(authViewModelProvider).session;
    final intakeLogsAsync = ref.watch(intakeLogsProvider);
    final now = DateTime.now().toLocal();

    final intakeLogs = intakeLogsAsync.valueOrNull ?? [];

    // Gom nhóm PENDING + OVERTIME + TAKEN của hôm nay.
    final pendingToday = intakeLogs
        .where((l) =>
            l.status.name == 'pending' || l.status.name == 'overtime')
        .where((l) => _isSameDay(l.scheduledTimeUtc.toLocal(), now))
        .toList();

    final overtimeToday = pendingToday
        .where((l) => l.status.name == 'overtime')
        .toList();
    final takenToday = intakeLogs
        .where((l) => l.status.name == 'taken')
        .where((l) => l.confirmedAtUtc != null)
        .where((l) => _isSameDay(l.confirmedAtUtc!.toLocal(), now))
        .toList();

    final totalToday = pendingToday.length + takenToday.length;
    final adherencePct = totalToday > 0
        ? (takenToday.length / totalToday * 100).round()
        : null;

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Header
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Xin chào,',
                        style: TextStyle(
                          fontSize: 15,
                          color: AppColors.muted,
                        ),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        session?.fullName ?? '',
                        style: const TextStyle(
                          fontSize: 22,
                          fontWeight: FontWeight.bold,
                          color: AppColors.navy,
                        ),
                      ),
                    ],
                  ),
                  Row(
                    children: [
                      const NotificationBell(),
                      // Profile button đã xóa - chuyển đến footer navigation
                    ],
                  ),
                ],
              ),
              const SizedBox(height: 20),

              // Card "Thuốc hôm nay"
              _TodayMedicationCard(
                adherencePct: adherencePct,
                pendingCount: pendingToday.length,
                overtimeCount: overtimeToday.length,
                takenCount: takenToday.length,
              ),
              const SizedBox(height: 20),

              // Grid lối tắt
              const _SectionTitle('Tiện ích'),
              const SizedBox(height: 12),
              _ShortcutGrid(
                shortcuts: [
                  _ShortcutItem(
                    icon: Icons.book_outlined,
                    label: 'Nhật ký sức khoẻ',
                    stub: false,
                    onTap: () => Navigator.of(context).push(
                      MaterialPageRoute<void>(
                        builder: (_) => const HealthLogScreen(),
                      ),
                    ),
                  ),
                  _ShortcutItem(
                    icon: Icons.calendar_today_outlined,
                    label: 'Đặt lịch khám',
                    stub: false,
                    onTap: () => Navigator.of(context).push(
                      MaterialPageRoute<void>(
                        builder: (_) => const BookAppointmentScreen(),
                      ),
                    ),
                  ),
                  _ShortcutItem(
                    icon: Icons.history_outlined,
                    label: 'Lịch sử khám',
                    stub: false,
                    onTap: () => Navigator.of(context).push(
                      MaterialPageRoute<void>(
                        builder: (_) => const MedicalRecordListScreen(),
                      ),
                    ),
                  ),
                  _ShortcutItem(
                    icon: Icons.article_outlined,
                    label: 'Bài viết sức khoẻ',
                    stub: false,
                    onTap: () => Navigator.of(context).push(
                      MaterialPageRoute<void>(
                        builder: (_) => const BlogListScreen(),
                      ),
                    ),
                  ),
                  _ShortcutItem(
                    icon: Icons.event_note_outlined,
                    label: 'Lịch khám của tôi',
                    stub: false,
                    onTap: () => Navigator.of(context).push(
                      MaterialPageRoute<void>(
                        builder: (_) => const MyAppointmentsScreen(),
                      ),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  bool _isSameDay(DateTime a, DateTime b) =>
      a.year == b.year && a.month == b.month && a.day == b.day;
}

/// Card hiển thị tổng tuân thủ hôm nay.
class _TodayMedicationCard extends StatelessWidget {
  const _TodayMedicationCard({
    required this.adherencePct,
    required this.pendingCount,
    required this.overtimeCount,
    required this.takenCount,
  });

  final int? adherencePct;
  final int pendingCount;
  final int overtimeCount;
  final int takenCount;

  @override
  Widget build(BuildContext context) {
    final allDone = pendingCount == 0 && takenCount > 0;

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Text(
                'Thuốc hôm nay',
                style: TextStyle(
                  fontSize: 15,
                  fontWeight: FontWeight.w600,
                  color: AppColors.navy,
                ),
              ),
              if (adherencePct != null)
                AdherencePillBadge(percent: adherencePct!.toDouble()),
            ],
          ),
          const SizedBox(height: 8),
          if (allDone)
            Text(
              'Tất cả liều đã được ghi nhận!',
              style: TextStyle(
                fontSize: 13,
                color: AppColors.success,
                fontWeight: FontWeight.w500,
              ),
            )
          else if (pendingCount > 0)
            overtimeCount > 0
                ? Text(
                    'Còn tổng cộng $pendingCount liều thuốc hôm nay, trong đó đang có $overtimeCount liều quá hạn!',
                    style: TextStyle(
                      fontSize: 13,
                      color: AppColors.danger,
                      fontWeight: FontWeight.w500,
                    ),
                  )
                : Text(
                    'Còn $pendingCount liều chưa uống hôm nay.',
                    style: TextStyle(
                      fontSize: 13,
                      color: AppColors.muted,
                    ),
                  )
          else
            Text(
              'Chưa có lịch uống thuốc hôm nay.',
              style: TextStyle(
                fontSize: 13,
                color: AppColors.muted,
              ),
            ),
          if (takenCount > 0) ...[
            const SizedBox(height: 4),
            Text(
              '$takenCount liều đã uống.',
              style: TextStyle(
                fontSize: 12,
                color: AppColors.success,
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _SectionTitle extends StatelessWidget {
  const _SectionTitle(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      style: const TextStyle(
        fontSize: 13,
        fontWeight: FontWeight.w600,
        color: AppColors.muted,
      ),
    );
  }
}

class _ShortcutItem {
  const _ShortcutItem({
    required this.icon,
    required this.label,
    this.stub = false,
    this.onTap,
  });

  final IconData icon;
  final String label;
  final bool stub;
  final VoidCallback? onTap;
}

class _ShortcutGrid extends StatelessWidget {
  const _ShortcutGrid({required this.shortcuts});

  final List<_ShortcutItem> shortcuts;

  @override
  Widget build(BuildContext context) {
    return GridView.builder(
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 2,
        mainAxisSpacing: 10,
        crossAxisSpacing: 10,
        childAspectRatio: 1.5,
      ),
      itemCount: shortcuts.length,
      itemBuilder: (context, index) {
        final item = shortcuts[index];
        return _ShortcutCard(
          icon: item.icon,
          label: item.label,
          stub: item.stub,
          onTap: item.onTap,
        );
      },
    );
  }
}

class _ShortcutCard extends StatelessWidget {
  const _ShortcutCard({
    required this.icon,
    required this.label,
    required this.stub,
    this.onTap,
  });

  final IconData icon;
  final String label;
  final bool stub;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: () {
        if (onTap != null) {
          onTap!();
        } else if (stub) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('Tính năng sắp ra mắt'),
              duration: Duration(seconds: 2),
            ),
          );
        }
      },
      borderRadius: BorderRadius.circular(16),
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: AppColors.border),
        ),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(icon, color: AppColors.teal, size: 26),
            const SizedBox(height: 6),
            Text(
              label,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w600,
                color: AppColors.navy,
              ),
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
            ),
          ],
        ),
      ),
    );
  }
}
