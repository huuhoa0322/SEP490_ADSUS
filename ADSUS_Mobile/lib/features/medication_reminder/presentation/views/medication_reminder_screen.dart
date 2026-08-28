import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_theme.dart';
import '../../../../shared/reminder_preference_store.dart';
import '../../domain/entities/intake_log.dart';
import '../viewmodels/intake_view_model.dart';
import '../widgets/adherence_pill_badge.dart';

/// Màn Thuốc — SCR-19 + cài đặt nhắc.
///
/// Giao diện bám sát mockup SCR-19:
///   - Header + trục 24 giờ (timeline)
///   - Card tổng tuân thủ + giờ nhắc tiếp theo
///   - Danh sách liều PENDING / TAKEN
///   - Phần cài đặt giờ nhắc cá nhân (local SharedPreferences)
///   - Thông tin đơn thuốc
///
/// Backend: GET /api/v1/me/medication-intakes → IntakeLog[]
///          POST /api/v1/me/medication-intakes/{id}/confirm
class MedicationReminderScreen extends ConsumerStatefulWidget {
  const MedicationReminderScreen({super.key});

  @override
  ConsumerState<MedicationReminderScreen> createState() =>
      _MedicationReminderScreenState();
}

class _MedicationReminderScreenState
    extends ConsumerState<MedicationReminderScreen> {
  Timer? _timer;
  late DateTime _now;

  @override
  void initState() {
    super.initState();
    _now = _nowInVn();
    _timer = Timer.periodic(const Duration(seconds: 30), (_) {
      if (mounted) setState(() => _now = _nowInVn());
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  /// Ép timezone UTC+7 (ICT — Indochina Time), không phụ thuộc máy ảo.
  static DateTime _nowInVn() =>
      DateTime.now().toUtc().add(const Duration(hours: 7));

  @override
  Widget build(BuildContext context) {
    final intakeLogsAsync = ref.watch(intakeLogsProvider);
    final prefAsync = ref.watch(reminderPreferenceProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: RefreshIndicator(
          onRefresh: () async => ref.invalidate(intakeLogsProvider),
          child: intakeLogsAsync.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (error, _) => _ErrorCard(
              message: error.toString(),
              onRetry: () => ref.invalidate(intakeLogsProvider),
            ),
            data: (logs) => _MedicationBody(logs: logs, now: _now, prefAsync: prefAsync),
          ),
        ),
      ),
    );
  }
}

class _MedicationBody extends ConsumerWidget {
  const _MedicationBody({
    required this.logs,
    required this.now,
    required this.prefAsync,
  });

  final List<IntakeLog> logs;
  final DateTime now;
  final AsyncValue<ReminderPreference> prefAsync;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final pending = logs.where((l) => l.status == IntakeStatus.pending).toList();
    final taken = logs.where((l) => l.status == IntakeStatus.taken).toList();
    final overtime = logs.where((l) => l.status == IntakeStatus.overtime).toList();

    final pendingToday = pending
        .where((l) => _isSameDay(l.scheduledTimeUtc.toLocal(), now))
        .toList();
    final takenToday = taken
        .where((l) =>
            l.confirmedAtUtc != null &&
            _isSameDay(l.confirmedAtUtc!.toLocal(), now))
        .toList();
    final overtimeToday = overtime
        .where((l) => _isSameDay(l.scheduledTimeUtc.toLocal(), now))
        .toList();

    // Adherence = taken / (taken + pending) — OVERTIME excluded from denominator (still pending)
    final adherencePct = (pendingToday.length + takenToday.length) > 0
        ? (takenToday.length / (pendingToday.length + takenToday.length) * 100).round()
        : null;

    final pref = prefAsync.valueOrNull ?? const ReminderPreference();

    String nextSlotLabel = '—';
    if (pref.notifEnabled) {
      final times = [pref.morningTime, pref.middayTime, pref.eveningTime]
        ..sort((a, b) {
          final aMin = a.hour * 60 + a.minute;
          final bMin = b.hour * 60 + b.minute;
          return aMin.compareTo(bMin);
        });
      final nowMin = now.hour * 60 + now.minute;
      for (final t in times) {
        final tMin = t.hour * 60 + t.minute;
        if (tMin > nowMin) {
          nextSlotLabel = _timeSlotLabel(t);
          break;
        }
      }
    }

    final nowFraction = (now.hour * 60 + now.minute) / (24 * 60);
    final markers = [
      _MarkerData(label: '07', hour: 7),
      _MarkerData(label: '08', hour: 8),
      _MarkerData(label: '12', hour: 12),
      _MarkerData(label: '20', hour: 20),
    ];

    return CustomScrollView(
      slivers: [
        SliverAppBar(
          backgroundColor: Colors.white,
          foregroundColor: AppColors.navy,
          pinned: true,
          title: const Text(
            'Nhắc uống thuốc',
            style: TextStyle(fontWeight: FontWeight.w600),
          ),
          automaticallyImplyLeading: false,
        ),
        SliverToBoxAdapter(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
            child: Column(
              children: [
                _DayAxisCard(
                  now: now,
                  nowFraction: nowFraction,
                  markers: markers,
                  takenToday: takenToday,
                  pendingToday: pendingToday,
                  overtimeToday: overtimeToday,
                ),
                const SizedBox(height: 12),
                _AdherenceSummaryCard(
                  adherencePct: adherencePct,
                  nextSlotLabel: nextSlotLabel,
                  takenToday: takenToday.length,
                  pendingToday: pendingToday.length,
                  overtimeToday: overtimeToday.length,
                ),
                const SizedBox(height: 16),
              ],
            ),
          ),
        ),
        if (pendingToday.isNotEmpty) ...[
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
              child: Text(
                'LIỀU CẦN UỐNG',
                style: TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w700,
                  color: AppColors.muted,
                  letterSpacing: 0.06,
                ),
              ),
            ),
          ),
          SliverList(
            delegate: SliverChildBuilderDelegate(
              (context, index) => Padding(
                padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
                child: _IntakePendingCard(log: pendingToday[index]),
              ),
              childCount: pendingToday.length,
            ),
          ),
        ],
        if (overtimeToday.isNotEmpty) ...[
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
              child: Row(
                children: [
                  const Icon(Icons.warning_amber, size: 14, color: AppColors.danger),
                  const SizedBox(width: 4),
                  Text(
                    'QUÁ GIỜ — CẦN UỐNG NGAY',
                    style: TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w700,
                      color: AppColors.danger,
                      letterSpacing: 0.06,
                    ),
                  ),
                ],
              ),
            ),
          ),
          SliverList(
            delegate: SliverChildBuilderDelegate(
              (context, index) => Padding(
                padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
                child: _IntakeOvertimeCard(log: overtimeToday[index]),
              ),
              childCount: overtimeToday.length,
            ),
          ),
        ],
        if (takenToday.isNotEmpty) ...[
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
              child: Text(
                'LIỀU ĐÃ UỐNG HÔM NAY',
                style: TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w700,
                  color: AppColors.muted,
                  letterSpacing: 0.06,
                ),
              ),
            ),
          ),
          SliverList(
            delegate: SliverChildBuilderDelegate(
              (context, index) => Padding(
                padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
                child: _IntakeTakenCard(log: takenToday[index]),
              ),
              childCount: takenToday.length,
            ),
          ),
        ],
        if (pendingToday.isEmpty && takenToday.isEmpty)
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.all(32),
              child: Center(
                child: Column(
                  children: [
                    Icon(Icons.medication_outlined,
                        size: 48, color: AppColors.muted.withValues(alpha: 0.5)),
                    const SizedBox(height: 12),
                    const Text(
                      'Chưa có lịch uống thuốc hôm nay',
                      style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.navy),
                    ),
                  ],
                ),
              ),
            ),
          ),
        SliverToBoxAdapter(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: _ReminderSettingsCard(),
          ),
        ),
      ],
    );
  }
}

class _MarkerData {
  const _MarkerData({required this.label, required this.hour});
  final String label;
  final int hour;
}

class _LegendDot extends StatelessWidget {
  const _LegendDot({required this.color, required this.label});
  final Color color;
  final String label;
  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 8,
          height: 8,
          decoration: BoxDecoration(
            color: color,
            borderRadius: BorderRadius.circular(2),
          ),
        ),
        const SizedBox(width: 4),
        Text(
          label,
          style: TextStyle(fontSize: 10, color: AppColors.muted),
        ),
      ],
    );
  }
}

class _DayAxisCard extends StatelessWidget {
  const _DayAxisCard({
    required this.now,
    required this.nowFraction,
    required this.markers,
    required this.takenToday,
    required this.pendingToday,
    required this.overtimeToday,
  });

  final DateTime now;
  final double nowFraction;
  final List<_MarkerData> markers;
  final List<IntakeLog> takenToday;
  final List<IntakeLog> pendingToday;
  final List<IntakeLog> overtimeToday;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'HÔM NAY · ${_formatDate(now)}',
                    style: TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w600,
                      color: AppColors.muted,
                      letterSpacing: 0.05,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    _formatTime(now),
                    style: const TextStyle(
                      fontSize: 22,
                      fontWeight: FontWeight.w700,
                      color: AppColors.navy,
                      fontFamily: 'monospace',
                    ),
                  ),
                ],
              ),
            ],
          ),
          const SizedBox(height: 16),
          SizedBox(
            height: 36,
            child: LayoutBuilder(
              builder: (context, constraints) {
                final w = constraints.maxWidth;
                return Stack(
                  clipBehavior: Clip.none,
                  children: [
                    // Grid
                    Row(
                      children: List.generate(24, (i) {
                        return Expanded(
                          child: Container(
                            decoration: BoxDecoration(
                              border: Border(
                                left: BorderSide(color: AppColors.border, width: 0.5),
                              ),
                            ),
                          ),
                        );
                      }),
                    ),
                    // Now dot
                    Positioned(
                      left: nowFraction * w - 6,
                      top: 2,
                      child: Column(
                        children: [
                          Container(
                            width: 10,
                            height: 10,
                            decoration: BoxDecoration(
                              shape: BoxShape.circle,
                              color: AppColors.danger,
                              boxShadow: [
                                BoxShadow(
                                  color: AppColors.danger.withValues(alpha: 0.4),
                                  blurRadius: 4,
                                ),
                              ],
                            ),
                          ),
                          const SizedBox(height: 2),
                          const Text(
                            'BÂY GIỜ',
                            style: TextStyle(
                              fontSize: 8,
                              fontWeight: FontWeight.w700,
                              color: AppColors.danger,
                            ),
                          ),
                        ],
                      ),
                    ),
                    // Markers
                    ...markers.map((m) {
                      final frac = m.hour / 24;
                      final taken = takenToday.any((l) => l.scheduledTimeUtc.toLocal().hour == m.hour);
                      final pending = pendingToday.any((l) => l.scheduledTimeUtc.toLocal().hour == m.hour);
                      final overtime = overtimeToday.any((l) => l.scheduledTimeUtc.toLocal().hour == m.hour);
                      Color color = AppColors.teal;
                      double opacity = 1.0;
                      if (taken) { color = AppColors.success; opacity = 0.55; }
                      if (overtime) { color = AppColors.danger; }
                      if (pending && !overtime) { color = AppColors.amberWarn; }
                      return Positioned(
                        left: frac * w - 11,
                        top: 2,
                        child: Container(
                          width: 22,
                          height: 26,
                          decoration: BoxDecoration(
                            color: color.withValues(alpha: opacity),
                            borderRadius: BorderRadius.circular(4),
                          ),
                          alignment: Alignment.center,
                          child: Text(
                            m.label,
                            style: const TextStyle(
                              fontSize: 10,
                              fontWeight: FontWeight.w700,
                              color: Colors.white,
                              fontFamily: 'monospace',
                            ),
                          ),
                        ),
                      );
                    }),
                  ],
                );
              },
            ),
          ),
          const SizedBox(height: 8),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: const [
              Text('00', style: TextStyle(fontSize: 10, fontFamily: 'monospace', color: AppColors.muted)),
              Text('06', style: TextStyle(fontSize: 10, fontFamily: 'monospace', color: AppColors.muted)),
              Text('12', style: TextStyle(fontSize: 10, fontFamily: 'monospace', color: AppColors.muted)),
              Text('18', style: TextStyle(fontSize: 10, fontFamily: 'monospace', color: AppColors.muted)),
              Text('24', style: TextStyle(fontSize: 10, fontFamily: 'monospace', color: AppColors.muted)),
            ],
          ),
          const SizedBox(height: 8),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              _LegendDot(color: AppColors.success, label: 'Đã uống'),
              const SizedBox(width: 16),
              _LegendDot(color: AppColors.amberWarn, label: 'Chưa đến giờ'),
              const SizedBox(width: 16),
              _LegendDot(color: AppColors.danger, label: 'Quá giờ'),
            ],
          ),
        ],
      ),
    );
  }
}

class _AdherenceSummaryCard extends StatelessWidget {
  const _AdherenceSummaryCard({
    required this.adherencePct,
    required this.nextSlotLabel,
    required this.takenToday,
    required this.pendingToday,
    required this.overtimeToday,
  });

  final int? adherencePct;
  final String nextSlotLabel;
  final int takenToday;
  final int pendingToday;
  final int overtimeToday;

  @override
  Widget build(BuildContext context) {
    final hasOvertime = overtimeToday > 0;
    final allDone = pendingToday == 0 && takenToday > 0 && !hasOvertime;

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(
          color: hasOvertime ? AppColors.danger.withValues(alpha: 0.5) : AppColors.border,
          width: hasOvertime ? 1.5 : 1,
        ),
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (hasOvertime) ...[
                  Row(
                    children: [
                      const Icon(Icons.warning_amber, size: 16, color: AppColors.danger),
                      const SizedBox(width: 6),
                      Expanded(
                        child: Text(
                          '$overtimeToday liều đã quá giờ!',
                          style: const TextStyle(
                            fontSize: 13,
                            fontWeight: FontWeight.w700,
                            color: AppColors.danger,
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 4),
                ],
                Text(
                  allDone
                      ? 'Tất cả liều đã được ghi nhận!'
                      : hasOvertime
                          ? 'Còn ${pendingToday > 0 ? "$pendingToday " : ""}liều chưa uống.'
                          : pendingToday > 0
                              ? 'Còn $pendingToday liều chưa uống hôm nay.'
                              : takenToday > 0
                                  ? '$takenToday liều đã uống hôm nay.'
                                  : 'Chưa có lịch uống thuốc hôm nay.',
                  style: TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w500,
                    color: hasOvertime ? AppColors.navy : (allDone ? AppColors.success : AppColors.navy),
                  ),
                ),
                if (nextSlotLabel != '—') ...[
                  const SizedBox(height: 4),
                  Text(
                    'Thông báo tiếp theo: $nextSlotLabel',
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.teal,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ],
              ],
            ),
          ),
          if (adherencePct != null)
            AdherencePillBadge(percent: adherencePct!.toDouble(), label: 'hôm nay'),
        ],
      ),
    );
  }
}

class _IntakePendingCard extends ConsumerWidget {
  const _IntakePendingCard({required this.log});

  final IntakeLog log;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final vmState = ref.watch(intakeListViewModelProvider);
    final viewModel = ref.read(intakeListViewModelProvider.notifier);
    final nowUtc = DateTime.now().toUtc();
    final canConfirm = !log.scheduledTimeUtc.isAfter(nowUtc);
    final localTime = log.scheduledTimeUtc.toLocal();
    final isSubmittingThis = vmState.isSubmittingFor(log.intakeId);

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border(
          left: BorderSide(
            color: canConfirm ? AppColors.amberWarn : AppColors.muted,
            width: 4,
          ),
        ),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.04),
            blurRadius: 4,
            offset: const Offset(0, 2),
          ),
        ],
      ),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                  decoration: BoxDecoration(
                    color: AppColors.teal.withValues(alpha: 0.1),
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Text(
                    _slotTimeLabel(localTime),
                    style: const TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
                      color: AppColors.teal,
                      fontFamily: 'monospace',
                    ),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        log.medicineName,
                        style: const TextStyle(
                          fontSize: 14,
                          fontWeight: FontWeight.w700,
                          color: AppColors.navy,
                        ),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        log.instructions != null
                            ? '${log.dosage} · ${log.instructions}'
                            : log.dosage,
                        style: TextStyle(
                          fontSize: 12,
                          color: AppColors.muted,
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
              ],
            ),
            const SizedBox(height: 12),
            SizedBox(
              width: double.infinity,
              child: ElevatedButton(
                onPressed: canConfirm && !isSubmittingThis
                    ? () => _confirm(context, ref, viewModel)
                    : null,
                style: ElevatedButton.styleFrom(
                  backgroundColor: AppColors.success,
                  disabledBackgroundColor: AppColors.muted.withValues(alpha: 0.2),
                  minimumSize: const Size.fromHeight(44),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(10),
                  ),
                ),
                child: isSubmittingThis
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          color: Colors.white,
                        ),
                      )
                    : const Text(
                        '✓ ĐÃ UỐNG',
                        style: TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w700,
                          color: Colors.white,
                        ),
                      ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _confirm(
    BuildContext context,
    WidgetRef ref,
    IntakeListViewModel viewModel,
  ) async {
    final ok = await viewModel.confirmIntake(log.intakeId);
    if (!ok && context.mounted) {
      final msg = ref.read(intakeListViewModelProvider).errorMessage;
      if (msg != null) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(msg), backgroundColor: AppColors.danger),
        );
        viewModel.clearError();
      }
    }
  }
}

class _IntakeOvertimeCard extends ConsumerWidget {
  const _IntakeOvertimeCard({required this.log});

  final IntakeLog log;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final vmState = ref.watch(intakeListViewModelProvider);
    final viewModel = ref.read(intakeListViewModelProvider.notifier);
    final localTime = log.scheduledTimeUtc.toLocal();
    final isSubmittingThis = vmState.isSubmittingFor(log.intakeId);

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: const Border(
          left: BorderSide(color: AppColors.danger, width: 4),
        ),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.04),
            blurRadius: 4,
            offset: const Offset(0, 2),
          ),
        ],
      ),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                  decoration: BoxDecoration(
                    color: AppColors.danger.withValues(alpha: 0.1),
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Text(
                    _slotTimeLabel(localTime),
                    style: const TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
                      color: AppColors.danger,
                      fontFamily: 'monospace',
                    ),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        log.medicineName,
                        style: const TextStyle(
                          fontSize: 14,
                          fontWeight: FontWeight.w700,
                          color: AppColors.navy,
                        ),
                      ),
                      const SizedBox(height: 2),
                      Row(
                        children: [
                          const Icon(Icons.schedule, size: 14, color: AppColors.danger),
                          const SizedBox(width: 4),
                          Text(
                            'Quá giờ · ',
                            style: const TextStyle(
                              fontSize: 12,
                              color: AppColors.danger,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                          Flexible(
                            child: Text(
                              log.instructions != null
                                  ? '${log.dosage} · ${log.instructions}'
                                  : log.dosage,
                              style: TextStyle(
                                fontSize: 12,
                                color: AppColors.muted,
                              ),
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            SizedBox(
              width: double.infinity,
              child: ElevatedButton(
                onPressed: !isSubmittingThis
                    ? () => _confirm(context, ref, viewModel)
                    : null,
                style: ElevatedButton.styleFrom(
                  backgroundColor: AppColors.success,
                  disabledBackgroundColor: AppColors.muted.withValues(alpha: 0.2),
                  minimumSize: const Size.fromHeight(44),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(10),
                  ),
                ),
                child: isSubmittingThis
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          color: Colors.white,
                        ),
                      )
                    : const Text(
                        '✓ ĐÃ UỐNG',
                        style: TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w700,
                          color: Colors.white,
                        ),
                      ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _confirm(
    BuildContext context,
    WidgetRef ref,
    IntakeListViewModel viewModel,
  ) async {
    final ok = await viewModel.confirmIntake(log.intakeId);
    if (!ok && context.mounted) {
      final msg = ref.read(intakeListViewModelProvider).errorMessage;
      if (msg != null) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(msg), backgroundColor: AppColors.danger),
        );
        viewModel.clearError();
      }
    }
  }
}

class _IntakeTakenCard extends StatelessWidget {
  const _IntakeTakenCard({required this.log});

  final IntakeLog log;

  @override
  Widget build(BuildContext context) {
    final localTime = log.confirmedAtUtc!.toLocal();

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: const Border(
          left: BorderSide(color: AppColors.success, width: 4),
        ),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.04),
            blurRadius: 4,
            offset: const Offset(0, 2),
          ),
        ],
      ),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Row(
          children: [
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
              decoration: BoxDecoration(
                color: AppColors.success.withValues(alpha: 0.1),
                borderRadius: BorderRadius.circular(8),
              ),
              child: Text(
                _slotTimeLabel(log.scheduledTimeUtc.toLocal()),
                style: const TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w700,
                  color: AppColors.success,
                  fontFamily: 'monospace',
                ),
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    log.medicineName,
                    style: const TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w700,
                      color: AppColors.navy,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    log.instructions != null
                        ? '${log.dosage} · ${log.instructions}'
                        : log.dosage,
                    style: TextStyle(
                      fontSize: 12,
                      color: AppColors.muted,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Row(
                    children: [
                      const Icon(Icons.check_circle, size: 14, color: AppColors.success),
                      const SizedBox(width: 4),
                      Text(
                        'Đã xác nhận lúc ${_formatTime(localTime)}',
                        style: const TextStyle(
                          fontSize: 12,
                          color: AppColors.success,
                          fontWeight: FontWeight.w500,
                          fontFamily: 'monospace',
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ReminderSettingsCard extends ConsumerWidget {
  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final prefAsync = ref.watch(reminderPreferenceProvider);
    final notifier = ref.read(reminderPreferenceProvider.notifier);

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColors.border),
      ),
      child: prefAsync.when(
        loading: () => const Padding(
          padding: EdgeInsets.all(16),
          child: Center(child: CircularProgressIndicator()),
        ),
        error: (e, st) => const SizedBox.shrink(),
        data: (pref) => Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 12),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Cài đặt nhắc thuốc',
                    style: TextStyle(
                      fontSize: 15,
                      fontWeight: FontWeight.w700,
                      color: AppColors.navy,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    'Giờ bật thông báo nhắc nhở. Giờ uống thuốc theo đơn bác sĩ kê.',
                    style: TextStyle(fontSize: 12, color: AppColors.muted),
                  ),
                ],
              ),
            ),
            const Divider(height: 1, color: AppColors.border),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Text(
                    'Bật thông báo',
                    style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.navy),
                  ),
                  Switch(
                    value: pref.notifEnabled,
                    activeTrackColor: AppColors.teal.withValues(alpha: 0.5),
                    activeThumbColor: AppColors.teal,
                    onChanged: (v) => notifier.setNotifEnabled(v),
                  ),
                ],
              ),
            ),
            if (pref.notifEnabled) ...[
              const Divider(height: 1, color: AppColors.border),
              _TimeSlotRow(
                label: 'Sáng',
                time: pref.morningTime,
                onPick: (t) => notifier.setMorningTime(t),
              ),
              const Divider(height: 1, color: AppColors.border),
              _TimeSlotRow(
                label: 'Trưa',
                time: pref.middayTime,
                onPick: (t) => notifier.setMiddayTime(t),
              ),
              const Divider(height: 1, color: AppColors.border),
              _TimeSlotRow(
                label: 'Tối',
                time: pref.eveningTime,
                onPick: (t) => notifier.setEveningTime(t),
              ),
            ],
            const SizedBox(height: 8),
          ],
        ),
      ),
    );
  }
}

class _TimeSlotRow extends StatelessWidget {
  const _TimeSlotRow({
    required this.label,
    required this.time,
    required this.onPick,
  });

  final String label;
  final TimeOfDay time;
  final ValueChanged<TimeOfDay> onPick;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: () => _pickTime(context),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text(
              label,
              style: const TextStyle(
                fontSize: 14,
                fontWeight: FontWeight.w600,
                color: AppColors.navy,
              ),
            ),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
              decoration: BoxDecoration(
                color: AppColors.background,
                borderRadius: BorderRadius.circular(8),
                border: Border.all(color: AppColors.border),
              ),
              child: Text(
                _formatTimeOfDay(time),
                style: const TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w700,
                  color: AppColors.navy,
                  fontFamily: 'monospace',
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _pickTime(BuildContext context) async {
    final messenger = ScaffoldMessenger.of(context);
    final picked = await showTimePicker(
      context: context,
      initialTime: time,
      builder: (context, child) {
        return Theme(
          data: Theme.of(context).copyWith(
            colorScheme: ColorScheme.light(
              primary: AppColors.teal,
              onPrimary: Colors.white,
              surface: Colors.white,
              onSurface: AppColors.navy,
            ),
          ),
          child: child!,
        );
      },
    );
    if (picked == null) return;

    final result = _clampToSlot(label, picked);
    if (result.adjusted) {
      final clamped = TimeOfDay(hour: result.hour, minute: result.minute);
      messenger.showSnackBar(
        SnackBar(
          content: Text(
            'Giờ $label phải từ ${_slotMinTime(label)} đến ${_slotMaxTime(label)}. '
            'Đã đặt về ${_formatTimeOfDay(clamped)}.',
          ),
          duration: const Duration(seconds: 3),
          backgroundColor: AppColors.amberWarn,
        ),
      );
      onPick(clamped);
    } else {
      onPick(picked);
    }
  }
}

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
              style: TextStyle(fontSize: 15, fontWeight: FontWeight.w600, color: AppColors.navy),
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

// ---- helpers ----

/// Kết quả clamp: giờ đã điều chỉnh + có hay không điều chỉnh.
class _ClampResult {
  const _ClampResult({required this.hour, required this.minute, required this.adjusted});
  final int hour;
  final int minute;
  final bool adjusted;
}

/// Clamp giờ vào khoảng hợp lệ của slot.
/// Sáng: 05:00–10:59 | Trưa: 11:00–16:59 | Tối: 17:00–23:59
///
/// Trả về _ClampResult.
/// adjusted = true khi giờ nằm ngoài khoảng và đã bị snap.
_ClampResult _clampToSlot(String label, TimeOfDay picked) {
  switch (label) {
    case 'Sáng': // 05:00–10:59
      if (picked.hour < 5) {
        return _ClampResult(hour: 5, minute: 0, adjusted: true);
      }
      if (picked.hour > 10 || (picked.hour == 10 && picked.minute > 59)) {
        return _ClampResult(hour: 10, minute: 59, adjusted: true);
      }
      return _ClampResult(hour: picked.hour, minute: picked.minute, adjusted: false);

    case 'Trưa': // 11:00–16:59
      if (picked.hour < 11) {
        return _ClampResult(hour: 11, minute: 0, adjusted: true);
      }
      if (picked.hour > 16 || (picked.hour == 16 && picked.minute > 59)) {
        return _ClampResult(hour: 16, minute: 59, adjusted: true);
      }
      return _ClampResult(hour: picked.hour, minute: picked.minute, adjusted: false);

    case 'Tối': // 17:00–23:59
      if (picked.hour < 17) {
        return _ClampResult(hour: 17, minute: 0, adjusted: true);
      }
      if (picked.hour > 23) {
        return _ClampResult(hour: 23, minute: 59, adjusted: true);
      }
      return _ClampResult(hour: picked.hour, minute: picked.minute, adjusted: false);

    default:
      return _ClampResult(hour: picked.hour, minute: picked.minute, adjusted: false);
  }
}

String _slotMinTime(String label) => switch (label) {
      'Sáng' => '05:00',
      'Trưa' => '11:00',
      'Tối' => '17:00',
      _ => ''
    };

String _slotMaxTime(String label) => switch (label) {
      'Sáng' => '10:59',
      'Trưa' => '16:59',
      'Tối' => '23:59',
      _ => ''
    };

bool _isSameDay(DateTime a, DateTime b) =>
    a.year == b.year && a.month == b.month && a.day == b.day;

String _formatDate(DateTime dt) {
  const months = [
    'Tháng 1','Tháng 2','Tháng 3','Tháng 4','Tháng 5','Tháng 6',
    'Tháng 7','Tháng 8','Tháng 9','Tháng 10','Tháng 11','Tháng 12',
  ];
  return '${dt.day.toString().padLeft(2, '0')}/${months[dt.month - 1]}, ${dt.year}';
}

String _formatTime(DateTime dt) =>
    '${dt.hour.toString().padLeft(2, '0')}:${dt.minute.toString().padLeft(2, '0')}';

String _formatTimeOfDay(TimeOfDay t) =>
    '${t.hour.toString().padLeft(2, '0')}:${t.minute.toString().padLeft(2, '0')}';

String _slotTimeLabel(DateTime dt) {
  final h = dt.hour;
  final m = dt.minute.toString().padLeft(2, '0');
  if (h < 12) return 'Sáng $h:$m';
  if (h < 18) return 'Trưa $h:$m';
  return 'Tối $h:$m';
}

String _timeSlotLabel(TimeOfDay t) {
  if (t.hour < 12) return 'Sáng ${_formatTimeOfDay(t)}';
  if (t.hour < 18) return 'Trưa ${_formatTimeOfDay(t)}';
  return 'Tối ${_formatTimeOfDay(t)}';
}
