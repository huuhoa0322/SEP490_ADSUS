import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_theme.dart';
import '../../../../core/utils/phone_number_rule.dart';
import '../viewmodels/add_relative_view_model.dart';

/// Màn hình Thêm người thân (Add Relative).
///
/// Chức năng:
///   - Nhập thông tin người thân: họ tên, SĐT, ngày sinh, nhãn quan hệ
///   - Kiểm tra SĐT đã đăng ký tài khoản chưa
///   - Lưu người thân mới
class AddRelativeScreen extends ConsumerStatefulWidget {
  const AddRelativeScreen({super.key});

  @override
  ConsumerState<AddRelativeScreen> createState() => _AddRelativeScreenState();
}

class _AddRelativeScreenState extends ConsumerState<AddRelativeScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _phoneController = TextEditingController();
  final _relationshipController = TextEditingController();

  @override
  void initState() {
    super.initState();
    // Reset state when screen opens
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(addRelativeViewModelProvider.notifier).reset();
    });
  }

  @override
  void dispose() {
    _nameController.dispose();
    _phoneController.dispose();
    _relationshipController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(addRelativeViewModelProvider);

    // Listen for state changes
    ref.listen<AddRelativeState>(addRelativeViewModelProvider, (prev, next) {
      // Navigate back on success
      if (prev?.savedRelative == null && next.savedRelative != null) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Đã thêm người thân thành công.'),
            backgroundColor: AppColors.teal,
          ),
        );
        Navigator.of(context).pop(true);
      }

      // Show error snackbar
      if (prev?.errorMessage == null && next.errorMessage != null && !next.isSaving) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(next.errorMessage!),
            backgroundColor: AppColors.danger,
            behavior: SnackBarBehavior.floating,
          ),
        );
      }
    });

    return Scaffold(
      appBar: AppBar(
        title: const Text('Thêm người thân'),
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(20),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                // Full Name
                _buildSectionLabel('HỌ TÊN NGƯỜI THÂN *'),
                TextFormField(
                  controller: _nameController,
                  decoration: const InputDecoration(
                    hintText: 'Nhập họ tên người thân',
                    prefixIcon: Icon(Icons.person_outline),
                  ),
                  textCapitalization: TextCapitalization.words,
                  onChanged: (value) => ref
                      .read(addRelativeViewModelProvider.notifier)
                      .updateFullName(value),
                  validator: (value) {
                    if (value == null || value.trim().isEmpty) {
                      return 'Vui lòng nhập họ tên';
                    }
                    return null;
                  },
                ),
                const SizedBox(height: 20),

                // Phone Number
                _buildSectionLabel('SỐ ĐIỆN THOẠI (TÙY CHỌN CHO NGƯỜI CAO TUỔI)'),
                TextFormField(
                  controller: _phoneController,
                  decoration: InputDecoration(
                    hintText: 'Nhập số điện thoại',
                    prefixIcon: const Icon(Icons.phone_outlined),
                    suffixIcon: state.isCheckingPhone
                        ? const Padding(
                            padding: EdgeInsets.all(12),
                            child: SizedBox(
                              width: 20,
                              height: 20,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            ),
                          )
                        : state.isPhoneChecked
                            ? Icon(
                                state.isPhoneRegistered
                                    ? Icons.error
                                    : Icons.check_circle,
                                color: state.isPhoneRegistered
                                    ? AppColors.danger
                                    : AppColors.teal,
                              )
                            : null,
                  ),
                  keyboardType: TextInputType.phone,
                  onChanged: (value) => ref
                      .read(addRelativeViewModelProvider.notifier)
                      .updatePhone(value),
                  validator: (value) {
                    if (value == null || value.trim().isEmpty) {
                      return null;
                    }
                    if (!PhoneNumberRule.isValid(value.trim())) {
                      return 'Số điện thoại không hợp lệ';
                    }
                    return null;
                  },
                ),

                // Phone warning if registered
                if (state.isPhoneRegistered) ...[
                  const SizedBox(height: 8),
                  Container(
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: AppColors.dangerTint,
                      borderRadius: BorderRadius.circular(8),
                      border: Border.all(color: AppColors.danger.withValues(alpha: 0.3)),
                    ),
                    child: const Row(
                      children: [
                        Icon(Icons.warning_amber, color: AppColors.danger, size: 20),
                        SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            'Số điện thoại này đã được đăng ký tài khoản. '
                            'Người đó có thể tự đặt lịch khám.',
                            style: TextStyle(
                              color: AppColors.danger,
                              fontSize: 13,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ],

                // Check phone button
                if (!state.isPhoneChecked && _phoneController.text.isNotEmpty) ...[
                  const SizedBox(height: 12),
                  OutlinedButton(
                    onPressed: () => ref
                        .read(addRelativeViewModelProvider.notifier)
                        .checkPhone(),
                    child: const Text('KIỂM TRA SỐ ĐIỆN THOẠI'),
                  ),
                ],
                const SizedBox(height: 20),

                // Date of Birth
                _buildSectionLabel('NGÀY SINH (TÙY CHỌN)'),
                InkWell(
                  onTap: () => _selectDateOfBirth(context),
                  borderRadius: BorderRadius.circular(28),
                  child: Container(
                    padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 18),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      border: Border.all(color: AppColors.border),
                      borderRadius: BorderRadius.circular(28),
                    ),
                    child: Row(
                      children: [
                        const Icon(Icons.cake_outlined, color: AppColors.muted),
                        const SizedBox(width: 12),
                        Text(
                          state.dateOfBirth != null
                              ? _formatDate(state.dateOfBirth!)
                              : 'Chọn ngày sinh',
                          style: TextStyle(
                            color: state.dateOfBirth != null
                                ? AppColors.navy
                                : AppColors.muted,
                          ),
                        ),
                        const Spacer(),
                        if (state.dateOfBirth != null)
                          IconButton(
                            icon: const Icon(Icons.clear, size: 20),
                            onPressed: () => ref
                                .read(addRelativeViewModelProvider.notifier)
                                .updateDateOfBirth(null),
                            padding: EdgeInsets.zero,
                            constraints: const BoxConstraints(),
                          ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 20),

                // Relationship Label
                _buildSectionLabel('Nhãn QUAN HỆ (TÙY CHỌN)'),
                TextFormField(
                  controller: _relationshipController,
                  decoration: const InputDecoration(
                    hintText: 'Ví dụ: Mẹ, Vợ, Con gái, Em trai...',
                    prefixIcon: Icon(Icons.family_restroom_outlined),
                  ),
                  onChanged: (value) => ref
                      .read(addRelativeViewModelProvider.notifier)
                      .updateRelationshipName(value),
                ),
                const SizedBox(height: 8),
                const Text(
                  'Nhãn giúp bạn phân biệt người thân với nhau.',
                  style: TextStyle(color: AppColors.muted, fontSize: 12),
                ),
                const SizedBox(height: 32),

                // Save Button
                ElevatedButton(
                  onPressed: state.canSave && !state.isSaving
                      ? () => _saveRelative()
                      : null,
                  child: state.isSaving
                      ? const SizedBox(
                          height: 20,
                          width: 20,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            color: Colors.white,
                          ),
                        )
                      : const Text('LƯU NGƯỜI THÂN'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildSectionLabel(String text) => Padding(
        padding: const EdgeInsets.only(bottom: 8, left: 4),
        child: Text(
          text,
          style: const TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w700,
            letterSpacing: 1.1,
            color: AppColors.navy,
          ),
        ),
      );

  Future<void> _selectDateOfBirth(BuildContext context) async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: ref.read(addRelativeViewModelProvider).dateOfBirth ?? DateTime(1980, 1, 1),
      firstDate: DateTime(1900),
      lastDate: DateTime(now.year - 1, 12, 31),
    );

    if (picked != null) {
      ref.read(addRelativeViewModelProvider.notifier).updateDateOfBirth(picked);
    }
  }

  Future<void> _saveRelative() async {
    if (!_formKey.currentState!.validate()) return;

    final success = await ref
        .read(addRelativeViewModelProvider.notifier)
        .saveRelative();

    if (!success && mounted) {
      // Error is handled by listener above
    }
  }

  String _formatDate(DateTime date) {
    return '${date.day.toString().padLeft(2, '0')}/'
        '${date.month.toString().padLeft(2, '0')}/'
        '${date.year}';
  }
}
