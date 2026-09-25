import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_theme.dart';
import '../../domain/entities/medical_record_summary.dart';
import '../viewmodels/medical_record_list_viewmodel.dart';
import 'medical_record_detail_screen.dart';

/// SCR-13 (Mobile) — danh sách lượt khám (UC-08), chia tab Bản thân / Người thân.
class MedicalRecordListScreen extends ConsumerWidget {
  const MedicalRecordListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(medicalRecordListViewModelProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Hồ sơ & Kết quả khám'),
        backgroundColor: AppColors.background,
        elevation: 0,
        foregroundColor: AppColors.navy,
      ),
      body: Column(
        children: [
          // ── SegmentedButton chuyển tab ──
          _FilterSegment(
            filterScope: state.filterScope,
            selfCount: state.selfCount,
            relativeCount: state.relativeCount,
            onChanged: (scope) => ref
                .read(medicalRecordListViewModelProvider.notifier)
                .setFilterScope(scope),
          ),
          // ── Nội dung danh sách ──
          Expanded(child: _buildBody(context, ref, state)),
        ],
      ),
    );
  }

  Widget _buildBody(
    BuildContext context,
    WidgetRef ref,
    MedicalRecordListState state,
  ) {
    if (state.isLoading && state.records.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }

    if (state.errorMessage != null && state.records.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                state.errorMessage!,
                textAlign: TextAlign.center,
                style: TextStyle(color: AppColors.muted),
              ),
              const SizedBox(height: 12),
              OutlinedButton(
                onPressed: () =>
                    ref.read(medicalRecordListViewModelProvider.notifier).load(),
                child: const Text('Thử lại'),
              ),
            ],
          ),
        ),
      );
    }

    if (state.records.isEmpty) {
      return _buildEmptyState(state.filterScope);
    }

    return RefreshIndicator(
      onRefresh: () => ref.read(medicalRecordListViewModelProvider.notifier).load(),
      child: ListView.separated(
        padding: const EdgeInsets.all(16),
        itemCount: state.records.length,
        separatorBuilder: (_, _) => const SizedBox(height: 12),
        itemBuilder: (context, index) {
          final record = state.records[index];
          return _MedicalRecordCard(
            record: record,
            isRelativeView: state.filterScope == 'RELATIVE',
            onTap: () => Navigator.of(context).push(
              MaterialPageRoute<void>(
                builder: (_) => MedicalRecordDetailScreen(caseId: record.caseId),
              ),
            ),
          );
        },
      ),
    );
  }

  Widget _buildEmptyState(String filterScope) {
    final isRelative = filterScope == 'RELATIVE';
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              isRelative ? Icons.people_outline : Icons.event_busy,
              size: 48,
              color: AppColors.muted,
            ),
            const SizedBox(height: 16),
            Text(
              isRelative
                  ? 'Chưa có hồ sơ khám nào của người thân.'
                  : 'Chưa có lượt khám nào hoàn tất (đã kê đơn thuốc).',
              textAlign: TextAlign.center,
              style: TextStyle(color: AppColors.muted),
            ),
          ],
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Widgets con
// ─────────────────────────────────────────────────────────────────────────────

/// Thanh chuyển tab Bản thân / Người thân — giống MyAppointmentsScreen.
class _FilterSegment extends StatelessWidget {
  const _FilterSegment({
    required this.filterScope,
    required this.selfCount,
    required this.relativeCount,
    required this.onChanged,
  });

  final String filterScope;
  final int selfCount;
  final int relativeCount;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
      child: SizedBox(
        width: double.infinity,
        child: SegmentedButton<String>(
          segments: [
            ButtonSegment<String>(
              value: 'SELF',
              label: Text('Hồ sơ của tôi ($selfCount)'),
              icon: const Icon(Icons.person, size: 16),
            ),
            ButtonSegment<String>(
              value: 'RELATIVE',
              label: Text('Người thân ($relativeCount)'),
              icon: const Icon(Icons.people, size: 16),
            ),
          ],
          selected: {filterScope},
          onSelectionChanged: (newSelection) {
            onChanged(newSelection.first);
          },
        ),
      ),
    );
  }
}

/// Thẻ hiển thị một lượt khám — kèm badge tên bệnh nhân + quan hệ khi ở tab người thân.
class _MedicalRecordCard extends StatelessWidget {
  const _MedicalRecordCard({
    required this.record,
    required this.isRelativeView,
    required this.onTap,
  });

  final MedicalRecordSummary record;
  final bool isRelativeView;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final formatted = '${record.visitDate.day.toString().padLeft(2, '0')}/'
        '${record.visitDate.month.toString().padLeft(2, '0')}/${record.visitDate.year}';

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(16),
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: AppColors.border),
        ),
        child: Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  // Badge người thân (chỉ hiện ở tab RELATIVE)
                  if (isRelativeView && record.patientName != null) ...[
                    _RelativeBadge(
                      patientName: record.patientName!,
                      relationshipName: record.relationshipName,
                    ),
                    const SizedBox(height: 8),
                  ],
                  // Ngày khám + trạng thái
                  Row(
                    children: [
                      Icon(Icons.event_note_outlined, color: AppColors.teal),
                      const SizedBox(width: 12),
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            formatted,
                            style: const TextStyle(
                              fontWeight: FontWeight.w600,
                              color: AppColors.navy,
                            ),
                          ),
                          const SizedBox(height: 2),
                          Text(
                            'Đã kê đơn',
                            style: TextStyle(fontSize: 12, color: AppColors.success),
                          ),
                        ],
                      ),
                    ],
                  ),
                ],
              ),
            ),
            Icon(Icons.chevron_right, color: AppColors.muted),
          ],
        ),
      ),
    );
  }
}

/// Badge tím hiển thị tên bệnh nhân + quan hệ (giống AppointmentCard).
class _RelativeBadge extends StatelessWidget {
  const _RelativeBadge({
    required this.patientName,
    this.relationshipName,
  });

  final String patientName;
  final String? relationshipName;

  @override
  Widget build(BuildContext context) {
    final label = relationshipName != null && relationshipName!.isNotEmpty
        ? '$relationshipName: $patientName'
        : patientName;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: Colors.purple.shade50,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: Colors.purple.shade200),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.people_alt_outlined, size: 13, color: Colors.purple.shade700),
          const SizedBox(width: 4),
          Flexible(
            child: Text(
              label,
              style: TextStyle(
                fontSize: 11,
                color: Colors.purple.shade700,
                fontWeight: FontWeight.w600,
              ),
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
          ),
        ],
      ),
    );
  }
}
