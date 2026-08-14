import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_theme.dart';
import '../viewmodels/medical_record_detail_viewmodel.dart';

/// SCR-14 (Mobile) — chi tiết 1 lượt khám (UC-08). KHÔNG có nút xuất PDF (Doctor/Nurse
/// only trên Web), KHÔNG có badge % AI confidence (GB-05) — xem thiết kế §1.
///
/// `ConsumerStatefulWidget` (không phải `ConsumerWidget` trơn) — cần `initState` để gọi
/// `loadDetail(caseId)` đúng một lần khi màn mở, vì ViewModel không dùng `.family`.
class MedicalRecordDetailScreen extends ConsumerStatefulWidget {
  const MedicalRecordDetailScreen({required this.caseId, super.key});

  final String caseId;

  @override
  ConsumerState<MedicalRecordDetailScreen> createState() =>
      _MedicalRecordDetailScreenState();
}

class _MedicalRecordDetailScreenState
    extends ConsumerState<MedicalRecordDetailScreen> {
  @override
  void initState() {
    super.initState();
    // Gọi sau frame đầu — tránh modify provider ngay trong lúc build.
    Future.microtask(
      () => ref
          .read(medicalRecordDetailViewModelProvider.notifier)
          .loadDetail(widget.caseId),
    );
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(medicalRecordDetailViewModelProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Chi tiết kết quả khám'),
        backgroundColor: AppColors.background,
        elevation: 0,
        foregroundColor: AppColors.navy,
      ),
      body: _buildBody(state),
    );
  }

  Widget _buildBody(MedicalRecordDetailState state) {
    if (state.errorMessage != null) {
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
                onPressed: () => ref
                    .read(medicalRecordDetailViewModelProvider.notifier)
                    .loadDetail(widget.caseId),
                child: const Text('Thử lại'),
              ),
            ],
          ),
        ),
      );
    }

    final record = state.caseDetail;
    if (record == null) {
      // Chưa load xong lần đầu (build() trả state rỗng trước khi initState's
      // microtask chạy) hoặc đang isLoading — hiện loading, không hiện màn trống giật hình.
      return const Center(child: CircularProgressIndicator());
    }

    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            '${record.visitDate.day.toString().padLeft(2, '0')}/'
            '${record.visitDate.month.toString().padLeft(2, '0')}/'
            '${record.visitDate.year}',
            style: const TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w600,
              color: AppColors.teal,
            ),
          ),
          const SizedBox(height: 12),
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(16),
              border: Border.all(color: AppColors.border),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Kết luận của bác sĩ',
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w700,
                    color: AppColors.muted,
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  record.conclusion ?? 'Chưa có kết luận.',
                  style: const TextStyle(fontSize: 14, color: AppColors.navy),
                ),
              ],
            ),
          ),
          if (record.prescriptionId != null) ...[
            const SizedBox(height: 12),
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: AppColors.teal.withValues(alpha: 0.08),
                borderRadius: BorderRadius.circular(16),
              ),
              child: Row(
                children: [
                  Icon(Icons.medication_outlined, color: AppColors.teal),
                  const SizedBox(width: 8),
                  Text(
                    'Có đơn thuốc kèm theo (${record.prescriptionStatus ?? "-"})',
                    style: TextStyle(fontSize: 13, color: AppColors.navy),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }
}
