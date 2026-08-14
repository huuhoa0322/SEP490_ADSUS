import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_theme.dart';
import '../../domain/entities/health_log.dart';
import '../viewmodels/health_log_view_model.dart';

/// Màn hình form thêm ghi chép sức khỏe mới (Module 9 - FT-35).
///
/// Giao diện:
///   - AppBar teal với tiêu đề "Thêm ghi chép"
///   - Form với GlobalKey FormState
///   - Type selector: hai nút EXERCISE / DIET với icons
///   - Content input: TextFormField với maxLines: 4, maxLength: 500
///   - Submit button full width với loading state
class AddHealthLogScreen extends ConsumerStatefulWidget {
  const AddHealthLogScreen({super.key});

  @override
  ConsumerState<AddHealthLogScreen> createState() => _AddHealthLogScreenState();
}

class _AddHealthLogScreenState extends ConsumerState<AddHealthLogScreen> {
  final _formKey = GlobalKey<FormState>();
  final _contentController = TextEditingController();

  HealthLogType _selectedType = HealthLogType.exercise;

  @override
  void dispose() {
    _contentController.dispose();
    super.dispose();
  }

  String get _hintText {
    return _selectedType == HealthLogType.exercise
        ? 'Ví dụ: Chạy bộ 30 phút...'
        : 'Ví dụ: Ăn salad rau...';
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;

    final viewModel = ref.read(healthLogViewModelProvider.notifier);
    final success = await viewModel.createLog(_selectedType, _contentController.text.trim());

    if (!mounted) return;

    if (success) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Đã lưu ghi chép!'),
          backgroundColor: AppColors.success,
        ),
      );
      Navigator.pop(context);
    } else {
      final errorMessage = ref.read(healthLogViewModelProvider).errorMessage;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(errorMessage ?? 'Đã xảy ra lỗi. Vui lòng thử lại.'),
          backgroundColor: AppColors.danger,
        ),
      );
      viewModel.clearError();
    }
  }

  @override
  Widget build(BuildContext context) {
    final vmState = ref.watch(healthLogViewModelProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Thêm ghi chép'),
        backgroundColor: AppColors.teal,
        foregroundColor: Colors.white,
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(20),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                // Type selector label
                const Text(
                  'Loại ghi chép',
                  style: TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w600,
                    color: AppColors.navy,
                    letterSpacing: 0.5,
                  ),
                ),
                const SizedBox(height: 12),

                // Type selector buttons
                Row(
                  children: [
                    Expanded(
                      child: _TypeButton(
                        type: HealthLogType.exercise,
                        isSelected: _selectedType == HealthLogType.exercise,
                        onTap: () => setState(() => _selectedType = HealthLogType.exercise),
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: _TypeButton(
                        type: HealthLogType.diet,
                        isSelected: _selectedType == HealthLogType.diet,
                        onTap: () => setState(() => _selectedType = HealthLogType.diet),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 24),

                // Content input
                const Text(
                  'Nội dung',
                  style: TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w600,
                    color: AppColors.navy,
                    letterSpacing: 0.5,
                  ),
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: _contentController,
                  maxLines: 4,
                  maxLength: 500,
                  enabled: !vmState.isSubmitting,
                  decoration: InputDecoration(
                    hintText: _hintText,
                    hintStyle: TextStyle(
                      color: AppColors.muted.withValues(alpha: 0.6),
                    ),
                    filled: true,
                    fillColor: Colors.white,
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(12),
                      borderSide: const BorderSide(color: AppColors.border),
                    ),
                    enabledBorder: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(12),
                      borderSide: const BorderSide(color: AppColors.border),
                    ),
                    focusedBorder: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(12),
                      borderSide: const BorderSide(color: AppColors.teal, width: 1.5),
                    ),
                    errorBorder: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(12),
                      borderSide: const BorderSide(color: AppColors.danger),
                    ),
                    focusedErrorBorder: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(12),
                      borderSide: const BorderSide(color: AppColors.danger, width: 1.5),
                    ),
                    counterStyle: const TextStyle(
                      color: AppColors.muted,
                      fontSize: 12,
                    ),
                  ),
                  validator: (value) {
                    if (value == null || value.trim().isEmpty) {
                      return 'Vui lòng nhập nội dung';
                    }
                    return null;
                  },
                ),
                const SizedBox(height: 32),

                // Submit button
                ElevatedButton(
                  onPressed: vmState.isSubmitting ? null : _submit,
                  style: ElevatedButton.styleFrom(
                    backgroundColor: AppColors.teal,
                    foregroundColor: Colors.white,
                    minimumSize: const Size.fromHeight(52),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(12),
                    ),
                    disabledBackgroundColor: AppColors.teal.withValues(alpha: 0.5),
                  ),
                  child: vmState.isSubmitting
                      ? const SizedBox(
                          width: 24,
                          height: 24,
                          child: CircularProgressIndicator(
                            strokeWidth: 2.5,
                            color: Colors.white,
                          ),
                        )
                      : const Text(
                          'Lưu ghi chép',
                          style: TextStyle(
                            fontSize: 15,
                            fontWeight: FontWeight.w600,
                            letterSpacing: 0.5,
                          ),
                        ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// Nut chon loai ghi chep.
///
/// Selected state: teal/navy background with white text.
/// Unselected state: white with teal/navy border and icon.
class _TypeButton extends StatelessWidget {
  const _TypeButton({
    required this.type,
    required this.isSelected,
    required this.onTap,
  });

  final HealthLogType type;
  final bool isSelected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final label = type == HealthLogType.exercise ? 'Tập thể dục' : 'Dinh dưỡng';
    final icon = type == HealthLogType.exercise ? Icons.directions_run : Icons.restaurant;

    final backgroundColor = isSelected ? AppColors.teal : Colors.white;
    final foregroundColor = isSelected ? Colors.white : AppColors.teal;
    final borderColor = AppColors.teal;

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(12),
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 200),
        padding: const EdgeInsets.symmetric(vertical: 14),
        decoration: BoxDecoration(
          color: backgroundColor,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: borderColor, width: 1.5),
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(icon, color: foregroundColor, size: 22),
            const SizedBox(width: 8),
            Text(
              label,
              style: TextStyle(
                fontSize: 14,
                fontWeight: FontWeight.w600,
                color: foregroundColor,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
