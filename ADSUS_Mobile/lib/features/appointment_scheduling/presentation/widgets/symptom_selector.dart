import 'package:flutter/material.dart';

import '../../../../core/theme/app_theme.dart';
import '../../domain/entities/symptom.dart';
import '../viewmodels/book_appointment_view_model.dart';

/// Widget cho phép chọn triệu chứng trước khi đặt lịch.
///
/// UI pattern:
/// - Các block, mỗi block là một category
/// - Block gồm:
///   - Dropdown chọn category (ẩn category đã chọn ở block khác)
///   - Grid checkbox hiển thị symptoms của category đó
///   - TextField nhập "Khác..." (nếu category có isOther)
///   - Nút xóa block (icon trash)
/// - Nút "Thêm nhóm triệu chứng" ở cuối
class SymptomSelector extends StatelessWidget {
  const SymptomSelector({
    super.key,
    required this.categories,
    required this.blocks,
    required this.isLoading,
    required this.onCategorySelected,
    required this.onSymptomToggled,
    required this.onOtherNoteChanged,
    required this.onBlockRemoved,
    required this.onAddBlock,
    required this.getUsedCategoryIds,
  });

  final List<SymptomCategory> categories;
  final List<SymptomBlock> blocks;
  final bool isLoading;
  final void Function(String blockId, String categoryId) onCategorySelected;
  final void Function(String blockId, String symptomId) onSymptomToggled;
  final void Function(String blockId, String note) onOtherNoteChanged;
  final void Function(String blockId) onBlockRemoved;
  final VoidCallback onAddBlock;
  final Set<String> Function(String currentBlockId) getUsedCategoryIds;

  @override
  Widget build(BuildContext context) {
    if (isLoading) {
      return const Center(
        child: Padding(
          padding: EdgeInsets.all(16),
          child: CircularProgressIndicator(),
        ),
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        // Header
        Row(
          children: [
            const Icon(Icons.medical_services_outlined, size: 20, color: AppColors.teal),
            const SizedBox(width: 8),
            const Text(
              'Triệu chứng (tùy chọn)',
              style: TextStyle(
                fontSize: 14,
                fontWeight: FontWeight.w600,
                color: AppColors.navy,
              ),
            ),
            const Spacer(),
            if (categories.isNotEmpty)
              TextButton.icon(
                onPressed: onAddBlock,
                icon: const Icon(Icons.add, size: 18),
                label: const Text('Thêm nhóm'),
                style: TextButton.styleFrom(
                  foregroundColor: AppColors.teal,
                  padding: EdgeInsets.zero,
                  minimumSize: Size.zero,
                  tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                ),
              ),
          ],
        ),
        const SizedBox(height: 8),

        // Blocks
        if (blocks.isEmpty)
          _buildEmptyState()
        else
          ...blocks.map((block) => _SymptomBlockWidget(
                block: block,
                categories: categories,
                usedCategoryIds: getUsedCategoryIds(block.id),
                onCategorySelected: (categoryId) => onCategorySelected(block.id, categoryId),
                onSymptomToggled: (symptomId) => onSymptomToggled(block.id, symptomId),
                onOtherNoteChanged: (note) => onOtherNoteChanged(block.id, note),
                onRemove: () => onBlockRemoved(block.id),
              )),
      ],
    );
  }

  Widget _buildEmptyState() {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.unreadBg,
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        children: [
          const Icon(Icons.info_outline, color: AppColors.muted, size: 32),
          const SizedBox(height: 8),
          const Text(
            'Chưa chọn triệu chứng',
            style: TextStyle(color: AppColors.muted, fontSize: 13),
          ),
          const SizedBox(height: 4),
          Text(
            'Nhấn "Thêm nhóm" để mô tả triệu chứng',
            style: TextStyle(color: AppColors.muted.withValues(alpha: 0.8), fontSize: 12),
          ),
          const SizedBox(height: 12),
          OutlinedButton.icon(
            onPressed: onAddBlock,
            icon: const Icon(Icons.add, size: 18),
            label: const Text('Thêm nhóm triệu chứng'),
            style: OutlinedButton.styleFrom(
              foregroundColor: AppColors.teal,
              side: const BorderSide(color: AppColors.teal),
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            ),
          ),
        ],
      ),
    );
  }
}

class _SymptomBlockWidget extends StatelessWidget {
  const _SymptomBlockWidget({
    required this.block,
    required this.categories,
    required this.usedCategoryIds,
    required this.onCategorySelected,
    required this.onSymptomToggled,
    required this.onOtherNoteChanged,
    required this.onRemove,
  });

  final SymptomBlock block;
  final List<SymptomCategory> categories;
  final Set<String> usedCategoryIds;
  final void Function(String) onCategorySelected;
  final void Function(String) onSymptomToggled;
  final void Function(String) onOtherNoteChanged;
  final VoidCallback onRemove;

  @override
  Widget build(BuildContext context) {
    final selectedCategory = categories.firstWhere(
      (c) => c.id == block.selectedCategoryId,
      orElse: () => const SymptomCategory(id: '', name: ''),
    );

    // Filter categories: ẩn category đã chọn ở block khác
    final availableCategories = categories
        .where((c) => !usedCategoryIds.contains(c.id))
        .toList();

    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Header with dropdown and remove button
          Row(
            children: [
              Expanded(
                child: DropdownButtonFormField<String>(
                  initialValue: block.selectedCategoryId,
                  decoration: InputDecoration(
                    labelText: 'Nhóm triệu chứng',
                    contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(8),
                    ),
                    isDense: true,
                  ),
                  isExpanded: true, // Prevent overflow
                  items: availableCategories.map((cat) {
                    return DropdownMenuItem(
                      value: cat.id,
                      child: Text(
                        cat.name,
                        overflow: TextOverflow.ellipsis,
                      ),
                    );
                  }).toList(),
                  onChanged: (value) {
                    if (value != null) {
                      onCategorySelected(value);
                    }
                  },
                  hint: const Text('Chọn nhóm'),
                ),
              ),
              const SizedBox(width: 8),
              IconButton(
                icon: const Icon(Icons.close, size: 20),
                color: AppColors.muted,
                onPressed: onRemove,
                tooltip: 'Xóa nhóm này',
                padding: EdgeInsets.zero,
                constraints: const BoxConstraints(minWidth: 32, minHeight: 32),
              ),
            ],
          ),

          // Symptoms grid
          if (block.selectedCategoryId != null && selectedCategory.symptoms.isNotEmpty) ...[
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: selectedCategory.symptoms.map((symptom) {
                final isSelected = block.selectedSymptomIds.contains(symptom.id);
                return FilterChip(
                  label: Text(
                    symptom.name,
                    style: TextStyle(
                      fontSize: 12,
                      color: isSelected ? Colors.white : AppColors.navy,
                    ),
                  ),
                  selected: isSelected,
                  onSelected: (_) => onSymptomToggled(symptom.id),
                  selectedColor: AppColors.teal,
                  checkmarkColor: Colors.white,
                  backgroundColor: AppColors.unreadBg,
                  side: BorderSide(
                    color: isSelected ? AppColors.teal : AppColors.border,
                  ),
                  padding: const EdgeInsets.symmetric(horizontal: 4),
                  visualDensity: VisualDensity.compact,
                );
              }).toList(),
            ),
          ],

          // Other note field
          if (block.selectedCategoryId != null) ...[
            const SizedBox(height: 12),
            TextField(
              decoration: InputDecoration(
                labelText: 'Mô tả khác (tùy chọn)',
                hintText: 'Nhập triệu chứng khác...',
                contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(8),
                ),
                isDense: true,
              ),
              style: const TextStyle(fontSize: 14),
              maxLines: 2,
              onChanged: onOtherNoteChanged,
            ),
          ],
        ],
      ),
    );
  }
}
