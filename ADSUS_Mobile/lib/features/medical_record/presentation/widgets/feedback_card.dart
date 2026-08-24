import 'package:flutter/material.dart';

import '../../../../core/theme/app_theme.dart';
import '../../domain/entities/medical_record_feedback.dart';

/// Card hiển thị feedback đã gửi (read-only, FT-37).
class FeedbackCard extends StatelessWidget {
  const FeedbackCard({super.key, required this.feedback});

  final MedicalRecordFeedback feedback;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.teal, width: 1.5),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Icon(Icons.rate_review_outlined, color: AppColors.teal, size: 18),
              const SizedBox(width: 6),
              Text(
                'Phản hồi về ca khám',
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w700,
                  color: AppColors.teal,
                ),
              ),
              const Spacer(),
              Text(
                _formatDate(feedback.submittedAt),
                style: TextStyle(fontSize: 11, color: AppColors.muted),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Row(
            children: List.generate(5, (i) {
              final star = i + 1;
              return Icon(
                star <= feedback.rating ? Icons.star : Icons.star_border,
                size: 20,
                color: star <= feedback.rating
                    ? AppColors.amberWarn
                    : AppColors.muted,
              );
            }),
          ),
          if (feedback.content != null && feedback.content!.isNotEmpty) ...[
            const SizedBox(height: 8),
            Text(
              feedback.content!,
              style: const TextStyle(fontSize: 13, color: AppColors.navy),
            ),
          ],
        ],
      ),
    );
  }

  String _formatDate(DateTime dt) {
    return '${dt.day.toString().padLeft(2, '0')}/'
        '${dt.month.toString().padLeft(2, '0')}/'
        '${dt.year}';
  }
}
