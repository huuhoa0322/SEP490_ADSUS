import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_theme.dart';
import '../../../../core/utils/phone_number_rule.dart';
import '../viewmodels/forgot_password_otp_view_model.dart';
import 'forgot_password_otp_verify_screen.dart';
import 'widgets/message_banner.dart';

/// Quên mật khẩu qua SMS OTP, bước 1/3 — chỉ áp dụng cho tài khoản Patient.
class ForgotPasswordOtpPhoneScreen extends ConsumerStatefulWidget {
  const ForgotPasswordOtpPhoneScreen({super.key});

  @override
  ConsumerState<ForgotPasswordOtpPhoneScreen> createState() => _ForgotPasswordOtpPhoneScreenState();
}

class _ForgotPasswordOtpPhoneScreenState extends ConsumerState<ForgotPasswordOtpPhoneScreen> {
  final _phoneController = TextEditingController();
  String? _clientError;

  @override
  void dispose() {
    _phoneController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final phone = _phoneController.text.trim();

    if (!PhoneNumberRule.isValid(phone)) {
      setState(() => _clientError = PhoneNumberRule.errorMessage);
      return;
    }

    setState(() => _clientError = null);
    final ok = await ref.read(forgotPasswordOtpViewModelProvider.notifier).requestOtp(phone);
    if (ok && mounted) {
      Navigator.of(context).push(
        MaterialPageRoute<void>(builder: (_) => const ForgotPasswordOtpVerifyScreen()),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(forgotPasswordOtpViewModelProvider);
    final message = _clientError ?? state.errorMessage;

    return Scaffold(
      appBar: AppBar(title: const Text('Quên mật khẩu (qua SMS)')),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(24, 20, 24, 32),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Text(
                'Nhập số điện thoại đã đăng ký. Hệ thống sẽ gửi mã xác thực qua SMS.',
                style: TextStyle(fontSize: 15, color: AppColors.muted, height: 1.5),
              ),
              const SizedBox(height: 28),

              const Text('SỐ ĐIỆN THOẠI',
                  style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, letterSpacing: 1.1, color: AppColors.navy)),
              const SizedBox(height: 8),
              TextField(
                controller: _phoneController,
                keyboardType: TextInputType.phone,
                enabled: !state.isSubmitting,
                onSubmitted: (_) => _submit(),
                decoration: const InputDecoration(
                  hintText: '0900000000',
                  prefixIcon: Icon(Icons.phone_outlined),
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
                    : const Text('GỬI MÃ XÁC THỰC'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
