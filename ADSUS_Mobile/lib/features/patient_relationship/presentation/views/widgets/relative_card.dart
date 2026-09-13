import 'package:flutter/material.dart';

import '../../../../core/theme/app_theme.dart';
import '../../domain/entities/patient_relationship.dart';

/// Card hiển thị thông tin một người thân trong danh sách.
class RelativeCard extends StatelessWidget {
  const RelativeCard({
    super.key,
    required this.relative,
    required this.isDeleting,
    required this.onDelete,
  });

  final PatientRelationship relative;
  final bool isDeleting;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: const BorderSide(color: AppColors.border),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () {
          // TODO: Navigate to edit screen or show details
        },
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              // Avatar với chữ cái đầu
              CircleAvatar(
                radius: 24,
                backgroundColor: AppColors.teal.withValues(alpha: 0.1),
                child: Text(
                  relative.fullName.isNotEmpty
                      ? relative.fullName[0].toUpperCase()
                      : '?',
                  style: const TextStyle(
                    fontSize: 20,
                    fontWeight: FontWeight.bold,
                    color: AppColors.teal,
                  ),
                ),
              ),
              const SizedBox(width: 16),

              // Thông tin
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      relative.fullName,
                      style: const TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w600,
                        color: AppColors.navy,
                      ),
                    ),
                    const SizedBox(height: 4),
                    if (relative.relationshipName != null &&
                        relative.relationshipName!.isNotEmpty)
                      Text(
                        relative.relationshipName!,
                        style: const TextStyle(
                          fontSize: 13,
                          color: AppColors.teal,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    const SizedBox(height: 2),
                    Row(
                      children: [
                        if (relative.phone != null &&
                            relative.phone!.isNotEmpty) ...[
                          const Icon(
                            Icons.phone_outlined,
                            size: 14,
                            color: AppColors.muted,
                          ),
                          const SizedBox(width: 4),
                          Text(
                            relative.phone!,
                            style: const TextStyle(
                              fontSize: 13,
                              color: AppColors.muted,
                            ),
                          ),
                        ],
                        if (relative.age != null) ...[
                          if (relative.phone != null &&
                              relative.phone!.isNotEmpty)
                            const SizedBox(width: 12),
                          const Icon(
                            Icons.cake_outlined,
                            size: 14,
                            color: AppColors.muted,
                          ),
                          const SizedBox(width: 4),
                          Text(
                            '${relative.age} tuổi',
                            style: const TextStyle(
                              fontSize: 13,
                              color: AppColors.muted,
                            ),
                          ),
                        ],
                      ],
                    ),
                  ],
                ),
              ),

              // Nút xóa
              IconButton(
                icon: isDeleting
                    ? const SizedBox(
                        width: 20,
                        height: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.delete_outline),
                color: AppColors.danger,
                onPressed: isDeleting ? null : onDelete,
                tooltip: 'Xóa người thân',
              ),
            ],
          ),
        ),
      ),
    );
  }
}
