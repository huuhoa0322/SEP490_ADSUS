import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_theme.dart';
import '../../domain/entities/intake_log.dart';
import '../viewmodels/intake_view_model.dart';
import '../widgets/adherence_pill_badge.dart';

/// SCR-19 — Lịch nhắc uống thuốc của bệnh nhân.
///
/// Giao diện bám sát mockup:
///   - Header card với tổng tuân thủ + ngày hôm nay
///   - Danh sách thuốc theo đơn, mỗi dòng có giờ uống + nút "Đã uống"
///
/// Backend: GET /api/v1/me/medication-intakes → IntakeLogResponse[]
///          POST /api/v1/me/medication-intakes/{id}/confirm
///
/// GB-01: status chỉ PENDING → TAKEN (một chiều). Nút "Đã uống" chỉ hiện
/// khi status = PENDING + scheduledTime <= now.
class MedicationReminderScreen extends ConsumerWidget {
  const MedicationReminderScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final intakeLogsAsync = ref.watch(intakeLogsProvider);
    final viewModel = ref.read(intakeListViewModelProvider.notifier);
    final vmState = ref.watch(intakeListViewModelProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Nhắc uống thuốc'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () => Navigator.of(context).pop(),
        ),
      ),
      body: intakeLogsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => _ErrorCard(
          message: error.toString(),
          onRetry: () => ref.invalidate(intakeLogsProvider),
        ),
        data: (logs) {
          if (logs.isEmpty) {
            return const _EmptyState();
          }

          // Tách PENDING (chưa uống) và TAKEN (đã uống).
          final pending = logs.where((l) => l.status.name == 'pending').toList();
          final taken = logs.where((l) => l.status.name == 'taken').toList();

          // Gom nhóm PENDING theo ngày (UTC → local).
          final pendingByDate = <String, List<IntakeLog>>{};
          for (final log in pending) {
            final dateKey = _dateLabel(log.scheduledTimeUtc.toLocal());
            pendingByDate.putIfAbsent(dateKey, () => []).add(log);
          }

          // Tính adherence hôm nay.
          final now = DateTime.now().toUtc();
          final todayLabel = _dateLabel(now.toLocal());
          final todayPending = (pendingByDate[todayLabel] ?? []).length;
          final todayTaken = taken.where((l) {
            final logDate = _dateLabel(l.confirmedAtUtc!.toLocal());
            return logDate == todayLabel;
          }).length;
          final totalToday = todayPending + todayTaken;
          final adherencePct = totalToday > 0 ? (todayTaken / totalToday * 100).round() : null;

          return RefreshIndicator(
            onRefresh: () async => ref.invalidate(intakeLogsProvider),
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                // Card tổng tuân thủ.
                _AdherenceSummaryCard(
                  adherencePct: adherencePct,
                  pendingCount: pending.length,
                  todayDate: todayLabel,
                ),

                const SizedBox(height: 16),

                // Danh sách thuốc theo ngày.
                for (final dateEntry in pendingByDate.entries) ...[
                  _DateSection(
                    dateLabel: dateEntry.key,
                    logs: dateEntry.value,
                    onConfirm: (intakeId) async {
                      final ok = await viewModel.confirmIntake(intakeId);
                      if (!ok && context.mounted) {
                        final msg = ref.read(intakeListViewModelProvider).errorMessage;
                        if (msg != null) {
                          ScaffoldMessenger.of(context).showSnackBar(
                            SnackBar(
                              content: Text(msg),
                              backgroundColor: AppColors.danger,
                            ),
                          );
                          viewModel.clearError();
                        }
                      }
                    },
                    isSubmitting: vmState.isSubmitting,
                  ),
                  const SizedBox(height: 12),
                ],

                // Thông báo đã uống hết hôm nay.
                if (pendingByDate.isEmpty)
                  const _AllDoneCard(),
              ],
            ),
          );
        },
      ),
    );
  }
}

/// Label ngày tiếng Việt.
String _dateLabel(DateTime dt) {
  final months = [
    'Tháng 1','Tháng 2','Tháng 3','Tháng 4','Tháng 5','Tháng 6',
    'Tháng 7','Tháng 8','Tháng 9','Tháng 10','Tháng 11','Tháng 12',
  ];
  final today = DateTime.now();
  if (dt.year == today.year && dt.month == today.month && dt.day == today.day) {
    return 'Hôm nay';
  }
  return '${dt.day} ${months[dt.month - 1]}, ${dt.year}';
}

/// Giờ tiếng Việt.
String _timeLabel(DateTime dt) {
  final h = dt.hour;
  final m = dt.minute.toString().padLeft(2, '0');
  if (h < 12) return 'Sáng $h:$m';
  if (h < 18) return 'Trưa $h:$m';
  return 'Tối $h:$m';
}

/// Card tổng tuân thủ.
class _AdherenceSummaryCard extends StatelessWidget {
  const _AdherenceSummaryCard({
    required this.adherencePct,
    required this.pendingCount,
    required this.todayDate,
  });

  final int? adherencePct;
  final int pendingCount;
  final String todayDate;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                todayDate,
                style: const TextStyle(
                  fontSize: 15,
                  fontWeight: FontWeight.w600,
                  color: AppColors.navy,
                ),
              ),
              AdherencePillBadge(
                percent: adherencePct?.toDouble(),
                label: 'hôm nay',
              ),
            ],
          ),
          const SizedBox(height: 12),
          Text(
            pendingCount == 0
                ? 'Tất cả thuốc đã được ghi nhận!'
                : 'Còn $pendingCount liều thuốc chưa uống hôm nay.',
            style: TextStyle(
              fontSize: 13,
              color: pendingCount == 0 ? AppColors.success : AppColors.muted,
            ),
          ),
        ],
      ),
    );
  }
}

/// Section theo ngày.
class _DateSection extends StatelessWidget {
  const _DateSection({
    required this.dateLabel,
    required this.logs,
    required this.onConfirm,
    required this.isSubmitting,
  });

  final String dateLabel;
  final List<IntakeLog> logs;
  final Future<void> Function(String intakeId) onConfirm;
  final bool isSubmitting;

  @override
  Widget build(BuildContext context) {
    // Sắp xếp theo giờ.
    final sorted = [...logs]..sort((a, b) =>
        a.scheduledTimeUtc.compareTo(b.scheduledTimeUtc));

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: const EdgeInsets.only(bottom: 8),
          child: Text(
            dateLabel,
            style: const TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w600,
              color: AppColors.muted,
            ),
          ),
        ),
        ...sorted.map((log) => _IntakeCard(
          log: log,
          onConfirm: () => onConfirm(log.intakeId),
          isSubmitting: isSubmitting,
        )),
      ],
    );
  }
}

/// Card 1 dòng thuốc.
class _IntakeCard extends StatelessWidget {
  const _IntakeCard({
    required this.log,
    required this.onConfirm,
    required this.isSubmitting,
  });

  final IntakeLog log;
  final VoidCallback onConfirm;
  final bool isSubmitting;

  @override
  Widget build(BuildContext context) {
    final now = DateTime.now().toUtc();
    final canConfirm = !log.scheduledTimeUtc.isAfter(now);

    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.border),
      ),
      child: Row(
        children: [
          // Giờ uống.
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
            decoration: BoxDecoration(
              color: AppColors.teal.withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(8),
            ),
            child: Text(
              _timeLabel(log.scheduledTimeUtc.toLocal()),
              style: const TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w700,
                color: AppColors.teal,
                fontFamily: 'JetBrains Mono',
              ),
            ),
          ),
          const SizedBox(width: 12),
          // Tên thuốc (mock — backend chưa trả medicineName).
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Thuốc #${log.intakeId.substring(0, 8)}',
                  style: const TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w600,
                    color: AppColors.navy,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  canConfirm ? 'Sẵn sàng uống' : 'Chưa đến giờ',
                  style: TextStyle(
                    fontSize: 12,
                    color: canConfirm ? AppColors.muted : AppColors.amberWarn,
                  ),
                ),
              ],
            ),
          ),
          // Nút xác nhận.
          SizedBox(
            width: 100,
            child: ElevatedButton(
              onPressed: canConfirm && !isSubmitting ? onConfirm : null,
              style: ElevatedButton.styleFrom(
                backgroundColor: AppColors.success,
                minimumSize: const Size(0, 40),
                padding: EdgeInsets.zero,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(20),
                ),
              ),
              child: isSubmitting
                  ? const SizedBox(
                      width: 16,
                      height: 16,
                      child: CircularProgressIndicator(
                        strokeWidth: 2,
                        color: Colors.white,
                      ),
                    )
                  : const Text(
                      'Đã uống',
                      style: TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.w700,
                        color: Colors.white,
                      ),
                    ),
            ),
          ),
        ],
      ),
    );
  }
}

/// Empty state — không có thuốc nào.
class _EmptyState extends StatelessWidget {
  const _EmptyState();

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              Icons.medication_outlined,
              size: 64,
              color: AppColors.muted.withValues(alpha: 0.5),
            ),
            const SizedBox(height: 16),
            const Text(
              'Chưa có lịch uống thuốc nào',
              style: TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.w600,
                color: AppColors.navy,
              ),
            ),
            const SizedBox(height: 8),
            const Text(
              'Khi bác sĩ kê đơn, lịch sẽ xuất hiện ở đây.',
              textAlign: TextAlign.center,
              style: TextStyle(fontSize: 13, color: AppColors.muted),
            ),
          ],
        ),
      ),
    );
  }
}

/// Tất cả đã uống xong.
class _AllDoneCard extends StatelessWidget {
  const _AllDoneCard();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(24),
      decoration: BoxDecoration(
        color: AppColors.success.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.success.withValues(alpha: 0.3)),
      ),
      child: const Row(
        children: [
          Icon(Icons.check_circle, color: AppColors.success, size: 32),
          SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Tuyệt vời!',
                  style: TextStyle(
                    fontSize: 15,
                    fontWeight: FontWeight.w700,
                    color: AppColors.success,
                  ),
                ),
                SizedBox(height: 2),
                Text(
                  'Bạn đã hoàn thành lịch uống thuốc hôm nay.',
                  style: TextStyle(fontSize: 13, color: AppColors.success),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Lỗi + nút retry.
class _ErrorCard extends StatelessWidget {
  const _ErrorCard({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.error_outline, color: AppColors.danger, size: 48),
            const SizedBox(height: 16),
            const Text(
              'Không tải được lịch thuốc',
              style: TextStyle(
                fontSize: 15,
                fontWeight: FontWeight.w600,
                color: AppColors.navy,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              message,
              textAlign: TextAlign.center,
              style: const TextStyle(fontSize: 13, color: AppColors.muted),
            ),
            const SizedBox(height: 16),
            OutlinedButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('Thử lại'),
            ),
          ],
        ),
      ),
    );
  }
}
