import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_theme.dart';
import '../viewmodels/forgot_password_otp_view_model.dart';
import 'widgets/message_banner.dart';

class ForgotPasswordOtpCompleteScreen extends ConsumerStatefulWidget {
  const ForgotPasswordOtpCompleteScreen({super.key});

  @override
  ConsumerState<ForgotPasswordOtpCompleteScreen> createState() => _ForgotPasswordOtpCompleteScreenState();
}

class _ForgotPasswordOtpCompleteScreenState extends ConsumerState<ForgotPasswordOtpCompleteScreen> {
  final _passwordController = TextEditingController();
  final _confirmPasswordController = TextEditingController();
  bool _obscurePassword = true;
  String? _clientError;

  @override
  void dispose() {
    _passwordController.dispose();
    _confirmPasswordController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final password = _passwordController.text;
    final confirmPassword = _confirmPasswordController.text;

    if (password.length < 8) {
      setState(() => _clientError = 'Mật khẩu phải có ít nhất 8 ký tự.');
      return;
    }
    if (password != confirmPassword) {
      setState(() => _clientError = 'Xác nhận mật khẩu không khớp.');
      return;
    }

    setState(() => _clientError = null);
    final ok = await ref.read(forgotPasswordOtpViewModelProvider.notifier).completeReset(
          newPassword: password,
          confirmNewPassword: confirmPassword,
        );

    // Giống RegisterCompleteScreen (Task 9) — thành công thì AuthGuard/router gốc tự nhận
    // token vừa ghi (completePasswordResetWithOtp) và điều hướng vào trang chủ.
    if (ok && mounted) {
      Navigator.of(context).popUntil((route) => route.isFirst);
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(forgotPasswordOtpViewModelProvider);
    final message = _clientError ?? state.errorMessage;

    return Scaffold(
      appBar: AppBar(title: const Text('Đặt mật khẩu mới')),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(24, 20, 24, 32),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Text(
                'Số điện thoại đã xác thực. Đặt mật khẩu mới để đăng nhập lại.',
                style: TextStyle(fontSize: 15, color: AppColors.muted, height: 1.5),
              ),
              const SizedBox(height: 28),

              const Text('MẬT KHẨU MỚI',
                  style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, letterSpacing: 1.1, color: AppColors.navy)),
              const SizedBox(height: 8),
              TextField(
                controller: _passwordController,
                obscureText: _obscurePassword,
                enabled: !state.isSubmitting,
                decoration: InputDecoration(
                  hintText: 'Ít nhất 8 ký tự, có chữ hoa và số',
                  prefixIcon: const Icon(Icons.lock_outline),
                  suffixIcon: IconButton(
                    icon: Icon(_obscurePassword ? Icons.visibility_outlined : Icons.visibility_off_outlined),
                    onPressed: () => setState(() => _obscurePassword = !_obscurePassword),
                  ),
                ),
              ),
              const SizedBox(height: 20),

              const Text('XÁC NHẬN MẬT KHẨU MỚI',
                  style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, letterSpacing: 1.1, color: AppColors.navy)),
              const SizedBox(height: 8),
              TextField(
                controller: _confirmPasswordController,
                obscureText: _obscurePassword,
                enabled: !state.isSubmitting,
                onSubmitted: (_) => _submit(),
                decoration: const InputDecoration(
                  hintText: '••••••••',
                  prefixIcon: Icon(Icons.lock_outline),
                ),
              ),

              if (message != null) ...[
                const SizedBox(height: 18),
                MessageBanner(message: message),
              ],

              const SizedBox(height: 26),
              ElevatedButton(
                onPressed: state.isSubmitting ? null : _submit,
                child: state.isSubmitting
                    ? const SizedBox(
                        height: 20, width: 20,
                        child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                    : const Text('ĐẶT LẠI MẬT KHẨU'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
