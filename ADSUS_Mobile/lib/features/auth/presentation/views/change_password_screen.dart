import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../core/theme/app_theme.dart';
import '../../../../shared/providers/app_providers.dart';
import '../viewmodels/auth_view_model.dart';
import 'widgets/message_banner.dart';

/// Chính sách mật khẩu — lấy từ TDS §4.3, phải khớp với validator phía backend.
/// Để ở đây để hiện yêu cầu ngay khi người dùng gõ, thay vì bắt họ gửi rồi mới báo lỗi.
const _passwordRules = <({String label, bool Function(String) test})>[
  (label: 'Từ 8 đến 72 ký tự', test: _lengthOk),
  (label: 'Có ít nhất 1 chữ hoa', test: _hasUpper),
  (label: 'Có ít nhất 1 chữ số', test: _hasDigit),
];

bool _lengthOk(String v) => v.length >= 8 && v.length <= 72;
bool _hasUpper(String v) => v.contains(RegExp(r'[A-Z]'));
bool _hasDigit(String v) => v.contains(RegExp(r'[0-9]'));

/// SCR-05 — màn đổi mật khẩu trên Mobile (UC-25).
class ChangePasswordScreen extends ConsumerStatefulWidget {
  const ChangePasswordScreen({super.key});

  @override
  ConsumerState<ChangePasswordScreen> createState() => _ChangePasswordScreenState();
}

class _ChangePasswordScreenState extends ConsumerState<ChangePasswordScreen> {
  final _currentController = TextEditingController();
  final _newController = TextEditingController();
  final _confirmController = TextEditingController();

  bool _isSaving = false;
  String? _errorMessage;
  bool _succeeded = false;

  @override
  void dispose() {
    _currentController.dispose();
    _newController.dispose();
    _confirmController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    // Bị ép đổi vì đang dùng mật khẩu tạm (sửa 06/08/2026) — không đòi mật khẩu hiện tại nữa,
    // người dùng vừa chứng minh biết giá trị đó qua bước đăng nhập ngay trước đây.
    final mustChange =
        ref.read(authViewModelProvider).session?.mustChangePassword ?? false;

    final current = _currentController.text;
    final newPassword = _newController.text;
    final confirm = _confirmController.text;

    if ((!mustChange && current.isEmpty) || newPassword.isEmpty || confirm.isEmpty) {
      setState(() => _errorMessage = mustChange
          ? 'Vui lòng điền đầy đủ cả hai ô.'
          : 'Vui lòng điền đầy đủ cả ba ô.');
      return;
    }
    if (_passwordRules.any((r) => !r.test(newPassword))) {
      setState(() => _errorMessage = 'Mật khẩu mới chưa đạt yêu cầu bên dưới.');
      return;
    }
    if (newPassword != confirm) {
      setState(() => _errorMessage = 'Xác nhận mật khẩu không khớp.');
      return;
    }

    setState(() {
      _isSaving = true;
      _errorMessage = null;
      _succeeded = false;
    });

    try {
      await ref.read(authRepositoryProvider).changePassword(
            currentPassword: mustChange ? null : current,
            newPassword: newPassword,
            confirmNewPassword: confirm,
          );

      // Backend đã gỡ cờ trong DB, gỡ luôn ở client để thôi chặn màn khác.
      ref.read(authViewModelProvider.notifier).clearMustChangePassword();

      if (mounted) setState(() => _succeeded = true);
    } on ApiException catch (e) {
      if (mounted) setState(() => _errorMessage = e.message);
    } finally {
      if (mounted) setState(() => _isSaving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final mustChange =
        ref.watch(authViewModelProvider).session?.mustChangePassword ?? false;
    final newPassword = _newController.text;
    final confirmMatches =
        _confirmController.text.isNotEmpty && _confirmController.text == newPassword;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Đổi mật khẩu'),
        // Người bị ép đổi mật khẩu thì không cho thoát ra (UC-25).
        automaticallyImplyLeading: !mustChange,
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(24, 16, 24, 32),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              if (mustChange) ...[
                const MessageBanner(
                  message: 'Tài khoản đang dùng mật khẩu tạm do phòng khám cấp. '
                      'Hãy đặt mật khẩu riêng để tiếp tục sử dụng.',
                  isError: false,
                ),
                const SizedBox(height: 22),
              ],

              // Bị ép đổi vì đang dùng mật khẩu tạm — không hỏi lại giá trị vừa dùng để đăng nhập.
              if (!mustChange) ...[
                _passwordField('MẬT KHẨU HIỆN TẠI', _currentController),
                const SizedBox(height: 18),
              ],
              _passwordField('MẬT KHẨU MỚI', _newController,
                  onChanged: (_) => setState(() {})),
              const SizedBox(height: 18),
              _passwordField('XÁC NHẬN MẬT KHẨU MỚI', _confirmController,
                  onChanged: (_) => setState(() {})),

              const SizedBox(height: 20),
              _buildChecklist(newPassword, confirmMatches),

              if (_errorMessage != null) ...[
                const SizedBox(height: 18),
                MessageBanner(message: _errorMessage!),
              ],
              if (_succeeded) ...[
                const SizedBox(height: 18),
                const MessageBanner(
                  message: 'Đổi mật khẩu thành công. Lần đăng nhập sau hãy dùng mật khẩu mới.',
                  isError: false,
                ),
              ],

              const SizedBox(height: 26),
              ElevatedButton(
                onPressed: _isSaving ? null : _submit,
                child: _isSaving
                    ? const SizedBox(
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(
                            strokeWidth: 2, color: Colors.white),
                      )
                    : const Text('ĐỔI MẬT KHẨU'),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _passwordField(
    String label,
    TextEditingController controller, {
    void Function(String)? onChanged,
  }) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: const TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w600,
            letterSpacing: 1.1,
            color: AppColors.navy,
          ),
        ),
        const SizedBox(height: 8),
        TextField(
          controller: controller,
          obscureText: true,
          enabled: !_isSaving,
          onChanged: onChanged,
          decoration: const InputDecoration(
            hintText: '••••••••',
            prefixIcon: Icon(Icons.lock_outline),
          ),
        ),
      ],
    );
  }

  Widget _buildChecklist(String newPassword, bool confirmMatches) {
    final items = <({String label, bool passed})>[
      for (final rule in _passwordRules)
        (label: rule.label, passed: rule.test(newPassword)),
      (label: 'Xác nhận trùng khớp với mật khẩu mới', passed: confirmMatches),
    ];

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 14),
      decoration: BoxDecoration(
        color: Colors.white,
        border: Border.all(color: AppColors.border),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          for (final item in items)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 4),
              child: Row(
                children: [
                  Icon(
                    item.passed ? Icons.check_circle : Icons.circle_outlined,
                    size: 18,
                    color: item.passed ? AppColors.teal : AppColors.border,
                  ),
                  const SizedBox(width: 10),
                  Text(
                    item.label,
                    style: TextStyle(
                      fontSize: 14,
                      color: item.passed ? AppColors.teal : AppColors.muted,
                    ),
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}
