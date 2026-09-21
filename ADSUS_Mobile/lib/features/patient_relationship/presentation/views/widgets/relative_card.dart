import 'package:flutter/material.dart';

import 'package:adsus_mobile/core/theme/app_theme.dart';
import 'package:adsus_mobile/core/utils/html_sanitizer.dart';
import 'package:adsus_mobile/features/patient_relationship/domain/entities/patient_relationship.dart';
import '../edit_relative_screen.dart';

/// Card hiển thị thông tin một người thân trong danh sách.
class RelativeCard extends StatelessWidget {
  const RelativeCard({
    super.key,
    required this.relative,
    this.isDeleting = false,
    this.onDelete,
    this.onEdit,
  });

  final PatientRelationship relative;
  final bool isDeleting;
  final VoidCallback? onDelete;
  final VoidCallback? onEdit;

  @override
  Widget build(BuildContext context) {
    final sanitizedName = HtmlSanitizer.sanitize(relative.fullName);
    final displayName =
        sanitizedName.isNotEmpty ? sanitizedName : 'Chưa có thông tin';
    final initial =
        displayName.isNotEmpty ? displayName[0].toUpperCase() : '?';

    final sanitizedRelationship =
        HtmlSanitizer.sanitize(relative.relationshipName);
    final sanitizedPhone = HtmlSanitizer.sanitize(relative.phone);

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
          showModalBottomSheet<void>(
            context: context,
            isScrollControlled: true,
            backgroundColor: Colors.transparent,
            builder: (context) => _RelativeDetailSheet(relative: relative),
          );
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
                  initial,
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
                      displayName,
                      style: const TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w600,
                        color: AppColors.navy,
                      ),
                    ),
                    const SizedBox(height: 4),
                    if (sanitizedRelationship.isNotEmpty)
                      Text(
                        sanitizedRelationship,
                        style: const TextStyle(
                          fontSize: 13,
                          color: AppColors.teal,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    const SizedBox(height: 2),
                    Row(
                      children: [
                        if (sanitizedPhone.isNotEmpty) ...[
                          const Icon(
                            Icons.phone_outlined,
                            size: 14,
                            color: AppColors.muted,
                          ),
                          const SizedBox(width: 4),
                          Text(
                            sanitizedPhone,
                            style: const TextStyle(
                              fontSize: 13,
                              color: AppColors.muted,
                            ),
                          ),
                        ],
                        if (relative.age != null) ...[
                          if (sanitizedPhone.isNotEmpty)
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

              // Nút sửa
              IconButton(
                icon: const Icon(Icons.edit_outlined),
                color: AppColors.teal,
                onPressed: onEdit ??
                    () async {
                      await Navigator.of(context).push(
                        MaterialPageRoute<PatientRelationship>(
                          builder: (_) =>
                              EditRelativeScreen(relative: relative),
                        ),
                      );
                      // result != null nghĩa là đã cập nhật thành công
                      // MyRelativesScreen sẽ tự rebuild vì updateRelativeLocally đã cập nhật state
                    },
                tooltip: 'Chỉnh sửa thông tin',
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Modal BottomSheet hiển thị thông tin chi tiết người thân với dữ liệu đã được làm sạch XSS.
typedef _RelativeDetailSheet = RelativeDetailSheet;

class RelativeDetailSheet extends StatelessWidget {
  const RelativeDetailSheet({super.key, required this.relative});

  final PatientRelationship relative;

  @override
  Widget build(BuildContext context) {
    final sanitizedName = HtmlSanitizer.sanitize(relative.fullName);
    final displayName =
        sanitizedName.isNotEmpty ? sanitizedName : 'Chưa có thông tin';
    final initial =
        displayName.isNotEmpty ? displayName[0].toUpperCase() : '?';

    final sanitizedRelationship =
        HtmlSanitizer.sanitize(relative.relationshipName);
    final relationshipText =
        sanitizedRelationship.isNotEmpty ? sanitizedRelationship : 'Chưa đặt nhãn';

    final phoneText = HtmlSanitizer.formatPhone(relative.phone);
    final dobText = HtmlSanitizer.formatDate(relative.dateOfBirth);
    final ageText =
        relative.age != null ? '${relative.age} tuổi' : 'Chưa cập nhật';
    final createdText = HtmlSanitizer.formatDate(relative.createdAt);

    return Container(
      decoration: const BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      padding: const EdgeInsets.fromLTRB(24, 16, 24, 32),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          // Handle bar
          Center(
            child: Container(
              width: 40,
              height: 4,
              margin: const EdgeInsets.only(bottom: 16),
              decoration: BoxDecoration(
                color: AppColors.border,
                borderRadius: BorderRadius.circular(2),
              ),
            ),
          ),

          // Title row with Close button
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Text(
                'Chi tiết người thân',
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.bold,
                  color: AppColors.navy,
                ),
              ),
              IconButton(
                icon: const Icon(Icons.close, color: AppColors.muted),
                onPressed: () => Navigator.of(context).pop(),
                tooltip: 'Đóng',
              ),
            ],
          ),
          const SizedBox(height: 8),

          // Header: Avatar, Name & Relationship badge
          Row(
            children: [
              CircleAvatar(
                radius: 28,
                backgroundColor: AppColors.teal.withValues(alpha: 0.1),
                child: Text(
                  initial,
                  style: const TextStyle(
                    fontSize: 24,
                    fontWeight: FontWeight.bold,
                    color: AppColors.teal,
                  ),
                ),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      displayName,
                      style: const TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.bold,
                        color: AppColors.navy,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Container(
                      padding:
                          const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                      decoration: BoxDecoration(
                        color: AppColors.teal.withValues(alpha: 0.1),
                        borderRadius: BorderRadius.circular(6),
                      ),
                      child: Text(
                        relationshipText,
                        style: const TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w600,
                          color: AppColors.teal,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          const Divider(),
          const SizedBox(height: 12),

          // Info Rows
          _DetailRow(
            icon: Icons.phone_outlined,
            label: 'Số điện thoại',
            value: phoneText,
          ),
          const SizedBox(height: 12),
          _DetailRow(
            icon: Icons.cake_outlined,
            label: 'Ngày sinh',
            value: dobText,
          ),
          const SizedBox(height: 12),
          _DetailRow(
            icon: Icons.calendar_today_outlined,
            label: 'Tuổi',
            value: ageText,
          ),
          const SizedBox(height: 12),
          _DetailRow(
            icon: Icons.history_outlined,
            label: 'Ngày tạo hồ sơ',
            value: createdText,
          ),

          const SizedBox(height: 24),
          ElevatedButton.icon(
            onPressed: () async {
              Navigator.of(context).pop();
              await Navigator.of(context).push(
                MaterialPageRoute<PatientRelationship>(
                  builder: (_) => EditRelativeScreen(relative: relative),
                ),
              );
              // updateRelativeLocally trong EditRelativeScreen đã cập nhật state
            },
            icon: const Icon(Icons.edit_outlined),
            label: const Text('Chỉnh sửa thông tin'),
          ),
          const SizedBox(height: 8),
          OutlinedButton(
            onPressed: () => Navigator.of(context).pop(),
            child: const Text('ĐÓNG'),
          ),
        ],
      ),
    );
  }
}

class _DetailRow extends StatelessWidget {
  const _DetailRow({
    required this.icon,
    required this.label,
    required this.value,
  });

  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Icon(icon, size: 20, color: AppColors.muted),
        const SizedBox(width: 12),
        SizedBox(
          width: 110,
          child: Text(
            label,
            style: const TextStyle(
              fontSize: 14,
              color: AppColors.muted,
            ),
          ),
        ),
        Expanded(
          child: Text(
            value,
            style: const TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w600,
              color: AppColors.navy,
            ),
          ),
        ),
      ],
    );
  }
}
