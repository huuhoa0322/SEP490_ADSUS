import 'package:flutter/material.dart';

import '../../../../core/theme/app_theme.dart';
import '../../domain/entities/health_log.dart';

/// Một thẻ nhật ký sức khỏe trong danh sách (Module 9 - FT-35).
///
/// Hiển thị:
///   - Icon theo loại (directions_run cho EXERCISE, restaurant cho DIET)
///   - Nhãn loại tiếng Việt ("Tập thể dục" / "Dinh dưỡng")
///   - Nội dung (tối đa 2 dòng, tràn nếu quá dài)
///   - Giờ tạo (định dạng HH:mm)
class HealthLogCard extends StatelessWidget {
  const HealthLogCard({super.key, required this.healthLog});

  final HealthLog healthLog;

  @override
  Widget build(BuildContext context) {
    final isExercise = healthLog.type == HealthLogType.exercise;
    final accentColor = isExercise ? AppColors.teal : AppColors.navy;
    final typeLabel = isExercise ? 'Tập thể dục' : 'Dinh dưỡng';
    final typeIcon = isExercise ? Icons.directions_run : Icons.restaurant;

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: AppColors.border),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Icon theo loai
          Container(
            width: 40,
            height: 40,
            decoration: BoxDecoration(
              color: accentColor.withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(10),
            ),
            child: Icon(
              typeIcon,
              color: accentColor,
              size: 22,
            ),
          ),
          const SizedBox(width: 12),

          // Noi dung
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                // Nhan loai + thoi gian
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 8,
                        vertical: 2,
                      ),
                      decoration: BoxDecoration(
                        color: accentColor.withValues(alpha: 0.1),
                        borderRadius: BorderRadius.circular(4),
                      ),
                      child: Text(
                        typeLabel,
                        style: TextStyle(
                          fontSize: 11,
                          fontWeight: FontWeight.w600,
                          color: accentColor,
                        ),
                      ),
                    ),
                    Text(
                      _formatTime(healthLog.createdAt),
                      style: TextStyle(
                        fontSize: 12,
                        color: AppColors.muted,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 8),

                // Noi dung
                Text(
                  healthLog.content,
                  style: TextStyle(
                    fontSize: 14,
                    color: AppColors.navy,
                    height: 1.4,
                  ),
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  String _formatTime(DateTime dateTime) {
    final local = dateTime.toLocal();
    return '${local.hour.toString().padLeft(2, '0')}:'
        '${local.minute.toString().padLeft(2, '0')}';
  }
}
