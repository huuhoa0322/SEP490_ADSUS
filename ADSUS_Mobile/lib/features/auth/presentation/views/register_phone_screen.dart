import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_theme.dart';
import '../../../../core/utils/phone_number_rule.dart';
import '../viewmodels/register_view_model.dart';
import 'widgets/message_banner.dart';
import 'register_otp_screen.dart';

/// Tự đăng ký, bước 1/3 — nhập số điện thoại để xin mã OTP.
class RegisterPhoneScreen extends ConsumerStatefulWidget {
  const RegisterPhoneScreen({super.key});

  @override
  ConsumerState<RegisterPhoneScreen> createState() => _RegisterPhoneScreenState();
}

class _RegisterPhoneScreenState extends ConsumerState<RegisterPhoneScreen> {
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
    final ok = await ref.read(registerViewModelProvider.notifier).requestOtp(phone);
    if (ok && mounted) {
      Navigator.of(context).push(
        MaterialPageRoute<void>(builder: (_) => const RegisterOtpScreen()),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(registerViewModelProvider);
    final message = _clientError ?? state.errorMessage;

    return Scaffold(
      appBar: AppBar(title: const Text('Đăng ký tài khoản')),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(24, 20, 24, 32),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Text(
                'Nhập số điện thoại của bạn. Hệ thống sẽ gửi mã xác thực qua SMS.',
                style: TextStyle(fontSize: 15, color: AppColors.muted, height: 1.5),
              ),
              const SizedBox(height: 28),

              const Text(
                'SỐ ĐIỆN THOẠI',
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w600,
                  letterSpacing: 1.1,
                  color: AppColors.navy,
                ),
              ),
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
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                      )
                    : const Text('GỬI MÃ XÁC THỰC'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
