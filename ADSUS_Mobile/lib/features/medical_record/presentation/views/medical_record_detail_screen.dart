import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_theme.dart';
import '../../domain/entities/medical_record_feedback.dart';
import '../../domain/entities/medical_record_image.dart';
import '../../domain/entities/medical_record_prescription.dart';
import '../viewmodels/medical_record_detail_viewmodel.dart';
import '../widgets/feedback_card.dart';
import 'feedback_sheet.dart';

/// SCR-14 (Mobile) — chi tiết 1 lượt khám (UC-08). KHÔNG có nút xuất PDF (Doctor/Nurse
/// only trên Web), KHÔNG có badge % AI confidence (GB-05) — xem thiết kế §1.
///
/// Đính chính 15/08/2026: giờ hiện ĐẦY ĐỦ nội dung như PDF export (chẩn đoán, tên bác sĩ,
/// hướng xử trí, đơn thuốc, ảnh siêu âm gốc) — quyết định 01/08/2026 thật sự chỉ ẩn NÚT XUẤT
/// FILE, không ẩn nội dung. Xem design spec 2026-08-15.
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
    Future.microtask(() async {
      ref
          .read(medicalRecordDetailViewModelProvider.notifier)
          .loadDetail(widget.caseId);
      // Load feedback song song với detail (FT-37).
      ref
          .read(medicalRecordDetailViewModelProvider.notifier)
          .loadFeedback(widget.caseId);
    });
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
    // Bug Important #2 fix (15/08/2026): state của case KHÁC (kể cả lỗi) không được lọt qua
    // đây — chặn NGAY trước mọi nhánh khác, thay vì chỉ so `caseDetail == null`.
    if (state.caseId != widget.caseId) {
      return const Center(child: CircularProgressIndicator());
    }

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
            '${record.visitDate.year}'
            ' · ${record.doctorName}',
            style: const TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w600,
              color: AppColors.teal,
            ),
          ),
          const SizedBox(height: 12),
          _InfoCard(title: 'Chẩn đoán', content: record.finalDiagnosis ?? '—'),
          const SizedBox(height: 12),
          _InfoCard(
            title: 'Hướng xử trí',
            content: record.doctorConclusion ?? 'Chưa có kết luận.',
          ),
          if (record.prescription != null) ...[
            const SizedBox(height: 12),
            _PrescriptionCard(prescription: record.prescription!),
          ],
          if (record.images.isNotEmpty) ...[
            const SizedBox(height: 20),
            Text(
              'Ảnh siêu âm',
              style: TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w700,
                color: AppColors.muted,
              ),
            ),
            const SizedBox(height: 8),
            _UltrasoundImageGrid(images: record.images),
          ],
          const SizedBox(height: 24),
          _FeedbackSection(
            caseId: widget.caseId,
            feedback: state.feedback,
            onSubmitFeedback: (rating, content) {
              ref
                  .read(medicalRecordDetailViewModelProvider.notifier)
                  .submitFeedback(widget.caseId, rating, content);
            },
          ),
        ],
      ),
    );
  }
}

class _FeedbackSection extends StatelessWidget {
  const _FeedbackSection({
    required this.caseId,
    required this.feedback,
    required this.onSubmitFeedback,
  });

  final String caseId;
  final MedicalRecordFeedback? feedback;
  final void Function(int rating, String? content) onSubmitFeedback;

  @override
  Widget build(BuildContext context) {
    if (feedback != null) {
      return FeedbackCard(feedback: feedback!);
    }
    return SizedBox(
      width: double.infinity,
      child: OutlinedButton.icon(
        onPressed: () => _openFeedbackSheet(context),
        icon: const Icon(Icons.rate_review_outlined),
        label: const Text('Phản hồi về ca khám'),
        style: OutlinedButton.styleFrom(
          foregroundColor: AppColors.teal,
          side: const BorderSide(color: AppColors.teal),
          padding: const EdgeInsets.symmetric(vertical: 12),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
          ),
        ),
      ),
    );
  }

  void _openFeedbackSheet(BuildContext context) {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (context) => FeedbackSheet(
        onSubmit: onSubmitFeedback,
      ),
    );
  }
}

class _InfoCard extends StatelessWidget {
  const _InfoCard({required this.title, required this.content});

  final String title;
  final String content;

  @override
  Widget build(BuildContext context) {
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
          Text(
            title,
            style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w700,
              color: AppColors.muted,
            ),
          ),
          const SizedBox(height: 8),
          Text(content, style: const TextStyle(fontSize: 14, color: AppColors.navy)),
        ],
      ),
    );
  }
}

/// Lưới ảnh siêu âm gốc — mirror `UltrasoundImageGallery` bên Web
/// (`adsus-fe/src/features/medical-record/components/ultrasound-image-gallery.tsx`), rút gọn
/// cho màn hình di động: lưới 2 cột, bấm vào mở full-screen zoom.
class _UltrasoundImageGrid extends StatelessWidget {
  const _UltrasoundImageGrid({required this.images});

  final List<MedicalRecordImage> images;

  @override
  Widget build(BuildContext context) {
    return GridView.builder(
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 2,
        crossAxisSpacing: 8,
        mainAxisSpacing: 8,
        childAspectRatio: 0.85,
      ),
      itemCount: images.length,
      itemBuilder: (context, index) {
        final image = images[index];
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: ClipRRect(
                borderRadius: BorderRadius.circular(12),
                child: image.imageUrl == null
                    ? Container(
                        color: AppColors.danger.withValues(alpha: 0.08),
                        padding: const EdgeInsets.all(8),
                        child: Center(
                          child: Text(
                            'Không tải được ảnh',
                            textAlign: TextAlign.center,
                            style: TextStyle(fontSize: 11, color: AppColors.danger),
                          ),
                        ),
                      )
                    : InkWell(
                        onTap: () => _openFullScreen(context, image.imageUrl!),
                        child: Image.network(
                          image.imageUrl!,
                          fit: BoxFit.cover,
                          errorBuilder: (context, error, stackTrace) => Container(
                            color: AppColors.danger.withValues(alpha: 0.08),
                            padding: const EdgeInsets.all(8),
                            child: Center(
                              child: Text(
                                'Không tải được ảnh',
                                textAlign: TextAlign.center,
                                style: TextStyle(fontSize: 11, color: AppColors.danger),
                              ),
                            ),
                          ),
                        ),
                      ),
              ),
            ),
            if (image.note != null) ...[
              const SizedBox(height: 4),
              Text(
                image.note!,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontSize: 10, color: AppColors.muted),
              ),
            ],
          ],
        );
      },
    );
  }

  void _openFullScreen(BuildContext context, String imageUrl) {
    showDialog<void>(
      context: context,
      barrierColor: Colors.black.withValues(alpha: 0.9),
      builder: (context) => GestureDetector(
        onTap: () => Navigator.of(context).pop(),
        child: InteractiveViewer(
          minScale: 1,
          maxScale: 4,
          child: Center(child: Image.network(imageUrl)),
        ),
      ),
    );
  }
}

class _PrescriptionCard extends StatelessWidget {
  const _PrescriptionCard({required this.prescription});

  final MedicalRecordPrescription prescription;

  @override
  Widget build(BuildContext context) {
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
            children: [
              Icon(Icons.medication_outlined, color: AppColors.teal),
              const SizedBox(width: 8),
              Text(
                'Đơn thuốc (${_statusLabel(prescription.status)})',
                style: const TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w700,
                  color: AppColors.muted,
                ),
              ),
            ],
          ),
          if (prescription.generalNote != null) ...[
            const SizedBox(height: 8),
            Text(
              prescription.generalNote!,
              style: const TextStyle(fontSize: 13, color: AppColors.navy),
            ),
          ],
          const SizedBox(height: 12),
          for (final item in prescription.items) ...[
            Text(
              item.medicineName,
              style: const TextStyle(
                fontSize: 14,
                fontWeight: FontWeight.w600,
                color: AppColors.navy,
              ),
            ),
            const SizedBox(height: 2),
            Text(
              '${item.dosage} · ${item.durationDays} ngày',
              style: TextStyle(fontSize: 12, color: AppColors.muted),
            ),
            if (item.instructions != null) ...[
              const SizedBox(height: 2),
              Text(
                item.instructions!,
                style: TextStyle(fontSize: 12, color: AppColors.muted),
              ),
            ],
            if (item != prescription.items.last) const SizedBox(height: 10),
          ],
        ],
      ),
    );
  }

  // Cùng 2 giá trị thật với _prescriptionStatusLabel (EnumExtensions.cs), tách riêng vì card
  // này là widget độc lập, không truy cập được method private của _MedicalRecordDetailScreenState.
  String _statusLabel(PrescriptionStatus status) => switch (status) {
        PrescriptionStatus.active => 'Đang dùng',
        PrescriptionStatus.completed => 'Đã hoàn thành',
      };
}
