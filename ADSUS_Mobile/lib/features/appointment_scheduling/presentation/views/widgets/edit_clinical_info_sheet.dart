import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../../core/theme/app_theme.dart';
import '../../../../../core/utils/html_sanitizer.dart';
import '../../../../../shared/providers/app_providers.dart';
import '../../../data/dtos/symptom_dtos.dart';
import '../../../domain/entities/appointment.dart';
import '../../../domain/entities/symptom.dart';
import '../../viewmodels/book_appointment_view_model.dart';
import '../../widgets/symptom_selector.dart';

/// Bottom sheet cho phép bệnh nhân chỉnh sửa lý do khám và triệu chứng lâm sàng.
class EditClinicalInfoSheet extends ConsumerStatefulWidget {
  const EditClinicalInfoSheet({
    super.key,
    required this.appointment,
    this.onUpdated,
  });

  final Appointment appointment;
  final void Function(Appointment updated)? onUpdated;

  @override
  ConsumerState<EditClinicalInfoSheet> createState() =>
      _EditClinicalInfoSheetState();
}

class _EditClinicalInfoSheetState extends ConsumerState<EditClinicalInfoSheet> {
  late final TextEditingController _reasonController;
  List<SymptomCategory> _categories = [];
  List<SymptomBlock> _blocks = [];
  bool _isLoadingCategories = true;
  bool _isSaving = false;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    _reasonController = TextEditingController(text: widget.appointment.reason ?? '');
    _loadCategories();
  }

  @override
  void dispose() {
    _reasonController.dispose();
    super.dispose();
  }

  Future<void> _loadCategories() async {
    try {
      final repo = ref.read(symptomRepositoryProvider);
      final categories = await repo.getCategories();

      final initialBlocks = <SymptomBlock>[];
      final oldSymptoms = widget.appointment.symptoms;

      if (oldSymptoms != null && oldSymptoms.isNotEmpty) {
        // Group by categoryId
        final grouped = <String, List<dynamic>>{};
        for (final s in oldSymptoms) {
          grouped.putIfAbsent(s.categoryId, () => []).add(s);
        }

        int index = 0;
        for (final entry in grouped.entries) {
          final catId = entry.key;
          final items = entry.value;

          final symptomIds = <String>{};
          String otherNote = '';

          for (final item in items) {
            if (item.symptomId != null && item.symptomId!.isNotEmpty) {
              symptomIds.add(item.symptomId!);
            }
            if (item.otherNote != null && item.otherNote!.isNotEmpty) {
              otherNote = item.otherNote!;
            }
          }

          initialBlocks.add(SymptomBlock(
            id: 'edit_block_${index++}_${DateTime.now().millisecondsSinceEpoch}',
            selectedCategoryId: catId,
            selectedSymptomIds: symptomIds,
            otherNote: otherNote,
          ));
        }
      }

      if (mounted) {
        setState(() {
          _categories = categories;
          _blocks = initialBlocks;
          _isLoadingCategories = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _isLoadingCategories = false;
          _errorMessage = 'Không tải được danh mục triệu chứng: $e';
        });
      }
    }
  }

  void _onCategorySelected(String blockId, String categoryId) {
    setState(() {
      _blocks = _blocks.map((b) {
        if (b.id != blockId) return b;
        return SymptomBlock(
          id: b.id,
          selectedCategoryId: categoryId,
          selectedSymptomIds: const {},
          otherNote: '',
        );
      }).toList();
    });
  }

  void _onSymptomToggled(String blockId, String symptomId) {
    setState(() {
      _blocks = _blocks.map((b) {
        if (b.id != blockId) return b;
        final newSet = Set<String>.from(b.selectedSymptomIds);
        if (newSet.contains(symptomId)) {
          newSet.remove(symptomId);
        } else {
          newSet.add(symptomId);
        }
        return b.copyWith(selectedSymptomIds: newSet);
      }).toList();
    });
  }

  void _onOtherNoteChanged(String blockId, String note) {
    setState(() {
      _blocks = _blocks.map((b) {
        if (b.id != blockId) return b;
        return b.copyWith(otherNote: note);
      }).toList();
    });
  }

  void _onBlockRemoved(String blockId) {
    setState(() {
      _blocks = _blocks.where((b) => b.id != blockId).toList();
    });
  }

  void _onAddBlock() {
    setState(() {
      _blocks = [
        ..._blocks,
        SymptomBlock(
          id: 'edit_block_${_blocks.length}_${DateTime.now().millisecondsSinceEpoch}',
        ),
      ];
    });
  }

  Set<String> _getUsedCategoryIds(String currentBlockId) {
    return _blocks
        .where((b) => b.id != currentBlockId && b.selectedCategoryId != null)
        .map((b) => b.selectedCategoryId!)
        .toSet();
  }

  Future<void> _saveChanges() async {
    final reasonText = _reasonController.text.trim();
    if (HtmlSanitizer.containsHtml(reasonText)) {
      setState(() {
        _errorMessage = 'Lý do khám không được chứa thẻ HTML.';
      });
      return;
    }

    setState(() {
      _isSaving = true;
      _errorMessage = null;
    });

    try {
      final repo = ref.read(appointmentRepositoryProvider);

      // Thu thập symptoms từ các blocks
      final symptomInputs = <SymptomInput>[];
      for (final block in _blocks) {
        symptomInputs.addAll(block.toSymptomInputs());
      }

      final updated = await repo.updateClinicalInfo(
        widget.appointment.id,
        reason: reasonText,
        symptoms: symptomInputs,
      );

      if (mounted) {
        Navigator.pop(context);
        widget.onUpdated?.call(updated);
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Cập nhật thông tin khám thành công!'),
            backgroundColor: AppColors.teal,
          ),
        );
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _isSaving = false;
          _errorMessage = e.toString();
        });
      }
    }
  }

  String _formatDate(DateTime? date) {
    if (date == null) return '—';
    return '${date.day.toString().padLeft(2, '0')}/'
        '${date.month.toString().padLeft(2, '0')}/'
        '${date.year}';
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      constraints: BoxConstraints(
        maxHeight: MediaQuery.of(context).size.height * 0.85,
      ),
      padding: EdgeInsets.only(
        left: 20,
        right: 20,
        top: 20,
        bottom: MediaQuery.of(context).viewInsets.bottom + 20,
      ),
      decoration: const BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
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

          // Header
          Row(
            children: [
              const Icon(Icons.edit_note, color: AppColors.teal, size: 24),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  'Sửa thông tin khám',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                        color: AppColors.navy,
                      ),
                ),
              ),
              IconButton(
                onPressed: () => Navigator.pop(context),
                icon: const Icon(Icons.close, size: 20),
                padding: EdgeInsets.zero,
                constraints: const BoxConstraints(),
              ),
            ],
          ),
          const SizedBox(height: 12),

          // Locked Doctor and Slot info (Read-only Card)
          Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: Colors.grey.shade50,
              borderRadius: BorderRadius.circular(10),
              border: Border.all(color: Colors.grey.shade200),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    const Icon(Icons.lock_outline, size: 14, color: AppColors.muted),
                    const SizedBox(width: 4),
                    Text(
                      'Bác sĩ & Thời gian cố định (không thể đổi)',
                      style: TextStyle(
                        fontSize: 11,
                        color: Colors.grey.shade600,
                        fontStyle: FontStyle.italic,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 6),
                Text(
                  'BS. ${widget.appointment.doctorName ?? '—'}',
                  style: const TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w600,
                    color: AppColors.navy,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  '${_formatDate(widget.appointment.slotDate)}  |  ${widget.appointment.startTime ?? '—'} - ${widget.appointment.endTime ?? '—'}',
                  style: const TextStyle(
                    fontSize: 13,
                    color: AppColors.muted,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),

          // Scrollable Form content
          Expanded(
            child: SingleChildScrollView(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  // Error alert
                  if (_errorMessage != null) ...[
                    Container(
                      padding: const EdgeInsets.all(10),
                      decoration: BoxDecoration(
                        color: Colors.red.shade50,
                        borderRadius: BorderRadius.circular(8),
                        border: Border.all(color: Colors.red.shade200),
                      ),
                      child: Row(
                        children: [
                          Icon(Icons.error_outline, size: 18, color: Colors.red.shade700),
                          const SizedBox(width: 8),
                          Expanded(
                            child: Text(
                              _errorMessage!,
                              style: TextStyle(
                                fontSize: 12,
                                color: Colors.red.shade700,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 12),
                  ],

                  // Lý do khám
                  const Text(
                    'Lý do khám',
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w600,
                      color: AppColors.navy,
                    ),
                  ),
                  const SizedBox(height: 6),
                  TextField(
                    controller: _reasonController,
                    maxLines: 2,
                    decoration: InputDecoration(
                      hintText: 'Nhập lý do hoặc vấn đề cần khám...',
                      hintStyle: const TextStyle(fontSize: 13, color: AppColors.muted),
                      contentPadding: const EdgeInsets.all(12),
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(8),
                        borderSide: const BorderSide(color: AppColors.border),
                      ),
                      enabledBorder: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(8),
                        borderSide: const BorderSide(color: AppColors.border),
                      ),
                      focusedBorder: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(8),
                        borderSide: const BorderSide(color: AppColors.teal),
                      ),
                    ),
                  ),
                  const SizedBox(height: 16),

                  // Triệu chứng lâm sàng
                  SymptomSelector(
                    categories: _categories,
                    blocks: _blocks,
                    isLoading: _isLoadingCategories,
                    onCategorySelected: _onCategorySelected,
                    onSymptomToggled: _onSymptomToggled,
                    onOtherNoteChanged: _onOtherNoteChanged,
                    onBlockRemoved: _onBlockRemoved,
                    onAddBlock: _onAddBlock,
                    getUsedCategoryIds: _getUsedCategoryIds,
                  ),
                  const SizedBox(height: 16),
                ],
              ),
            ),
          ),

          // Save button
          ElevatedButton(
            onPressed: _isSaving ? null : _saveChanges,
            style: ElevatedButton.styleFrom(
              backgroundColor: AppColors.teal,
              foregroundColor: Colors.white,
              padding: const EdgeInsets.symmetric(vertical: 14),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(10),
              ),
            ),
            child: _isSaving
                ? const SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(
                      strokeWidth: 2,
                      valueColor: AlwaysStoppedAnimation<Color>(Colors.white),
                    ),
                  )
                : const Text(
                    'Lưu thay đổi',
                    style: TextStyle(fontSize: 15, fontWeight: FontWeight.bold),
                  ),
          ),
        ],
      ),
    );
  }
}
