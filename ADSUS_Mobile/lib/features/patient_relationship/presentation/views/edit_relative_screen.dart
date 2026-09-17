import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../core/theme/app_theme.dart';
import '../../../../core/utils/html_sanitizer.dart';
import '../../../../core/utils/phone_number_rule.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../domain/entities/patient_relationship.dart';
import '../viewmodels/my_relatives_view_model.dart';

/// Màn hình Chỉnh sửa thông tin người thân (Edit Relative).
///
/// Chức năng:
///   - Pre-fill thông tin hiện tại: họ tên, SĐT, ngày sinh, nhãn quan hệ
///   - Validate form: họ tên không chứa HTML, SĐT hợp lệ, nhãn quan hệ không chứa HTML
///   - Cập nhật thông tin qua API và cập nhật danh sách local qua MyRelativesViewModel
class EditRelativeScreen extends ConsumerStatefulWidget {
  const EditRelativeScreen({
    super.key,
    required this.relative,
  });

  final PatientRelationship relative;

  @override
  ConsumerState<EditRelativeScreen> createState() => _EditRelativeScreenState();
}

class _EditRelativeScreenState extends ConsumerState<EditRelativeScreen> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _nameController;
  late final TextEditingController _phoneController;
  late final TextEditingController _relationshipController;
  DateTime? _dateOfBirth;
  bool _isSaving = false;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    _nameController = TextEditingController(text: widget.relative.fullName);
    _phoneController = TextEditingController(text: widget.relative.phone ?? '');
    _relationshipController =
        TextEditingController(text: widget.relative.relationshipName ?? '');
    _dateOfBirth = widget.relative.dateOfBirth;
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
    return Scaffold(
      appBar: AppBar(
        title: const Text('Chỉnh sửa thông tin'),
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(20),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (_errorMessage != null) ...[
                  Container(
                    padding: const EdgeInsets.all(12),
                    margin: const EdgeInsets.only(bottom: 16),
                    decoration: BoxDecoration(
                      color: AppColors.dangerTint,
                      borderRadius: BorderRadius.circular(8),
                      border: Border.all(
                        color: AppColors.danger.withValues(alpha: 0.3),
                      ),
                    ),
                    child: Row(
                      children: [
                        const Icon(Icons.error_outline,
                            color: AppColors.danger, size: 20),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            _errorMessage!,
                            style: const TextStyle(
                              color: AppColors.danger,
                              fontSize: 13,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ],

                // Full Name
                _buildSectionLabel('HỌ TÊN NGƯỜI THÂN *'),
                TextFormField(
                  controller: _nameController,
                  decoration: const InputDecoration(
                    hintText: 'Nhập họ tên người thân',
                    prefixIcon: Icon(Icons.person_outline),
                  ),
                  textCapitalization: TextCapitalization.words,
                  validator: (value) {
                    if (value == null || value.trim().isEmpty) {
                      return 'Vui lòng nhập họ tên';
                    }
                    if (HtmlSanitizer.containsHtml(value)) {
                      return 'Họ tên không được chứa thẻ HTML';
                    }
                    return null;
                  },
                ),
                const SizedBox(height: 20),

                // Phone Number
                _buildSectionLabel('SỐ ĐIỆN THOẠI (TÙY CHỌN CHO NGƯỜI CAO TUỔI)'),
                TextFormField(
                  controller: _phoneController,
                  decoration: const InputDecoration(
                    hintText: 'Nhập số điện thoại',
                    prefixIcon: Icon(Icons.phone_outlined),
                  ),
                  keyboardType: TextInputType.phone,
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
                const SizedBox(height: 20),

                // Date of Birth
                _buildSectionLabel('NGÀY SINH (TÙY CHỌN)'),
                InkWell(
                  onTap: _isSaving ? null : () => _selectDateOfBirth(context),
                  borderRadius: BorderRadius.circular(28),
                  child: Container(
                    padding: const EdgeInsets.symmetric(
                        horizontal: 20, vertical: 18),
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
                          _dateOfBirth != null
                              ? _formatDate(_dateOfBirth!)
                              : 'Chọn ngày sinh',
                          style: TextStyle(
                            color: _dateOfBirth != null
                                ? AppColors.navy
                                : AppColors.muted,
                          ),
                        ),
                        const Spacer(),
                        if (_dateOfBirth != null)
                          IconButton(
                            icon: const Icon(Icons.clear, size: 20),
                            onPressed: _isSaving
                                ? null
                                : () => setState(() => _dateOfBirth = null),
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
                  validator: (value) {
                    if (value != null &&
                        value.trim().isNotEmpty &&
                        HtmlSanitizer.containsHtml(value)) {
                      return 'Nhãn quan hệ không được chứa thẻ HTML';
                    }
                    return null;
                  },
                ),
                const SizedBox(height: 8),
                const Text(
                  'Nhãn giúp bạn phân biệt người thân với nhau.',
                  style: TextStyle(color: AppColors.muted, fontSize: 12),
                ),
                const SizedBox(height: 32),

                // Save Button
                ElevatedButton(
                  onPressed: _isSaving ? null : _saveChanges,
                  child: _isSaving
                      ? const SizedBox(
                          height: 20,
                          width: 20,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            color: Colors.white,
                          ),
                        )
                      : const Text('CẬP NHẬT THÔNG TIN'),
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
      initialDate: _dateOfBirth ?? DateTime(1980, 1, 1),
      firstDate: DateTime(1900),
      lastDate: DateTime(now.year - 1, 12, 31),
    );

    if (picked != null) {
      setState(() {
        _dateOfBirth = picked;
      });
    }
  }

  Future<void> _saveChanges() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() {
      _isSaving = true;
      _errorMessage = null;
    });

    try {
      final repo = ref.read(patientRelationshipRepositoryProvider);
      final fullName = _nameController.text.trim();
      final phone = _phoneController.text.trim().isNotEmpty
          ? _phoneController.text.trim()
          : null;
      final relationshipName = _relationshipController.text.trim().isNotEmpty
          ? _relationshipController.text.trim()
          : null;

      final updated = await repo.updateRelative(
        widget.relative.relationshipId,
        fullName: fullName,
        phone: phone,
        dateOfBirth: _dateOfBirth,
        relationshipName: relationshipName,
      );

      // Force reload toàn bộ danh sách từ API để đảm bảo UI hiển thị đúng
      ref.read(myRelativesViewModelProvider.notifier).loadRelatives();

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Cập nhật thông tin thành công.'),
            backgroundColor: AppColors.teal,
          ),
        );
        Navigator.of(context).pop(updated);
      }
    } on ApiException catch (e) {
      if (mounted) {
        setState(() {
          _isSaving = false;
          _errorMessage = e.message;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _isSaving = false;
          _errorMessage = 'Cập nhật thông tin thất bại. Vui lòng thử lại.';
        });
      }
    }
  }

  String _formatDate(DateTime date) {
    return '${date.day.toString().padLeft(2, '0')}/'
        '${date.month.toString().padLeft(2, '0')}/'
        '${date.year}';
  }
}
