import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_theme.dart';
import '../viewmodels/forgot_password_otp_view_model.dart';
import 'forgot_password_otp_complete_screen.dart';
import 'widgets/message_banner.dart';

class ForgotPasswordOtpVerifyScreen extends ConsumerStatefulWidget {
  const ForgotPasswordOtpVerifyScreen({super.key});

  @override
  ConsumerState<ForgotPasswordOtpVerifyScreen> createState() => _ForgotPasswordOtpVerifyScreenState();
}

class _ForgotPasswordOtpVerifyScreenState extends ConsumerState<ForgotPasswordOtpVerifyScreen> {
  final _otpController = TextEditingController();
  String? _clientError;

  @override
  void dispose() {
    _otpController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final code = _otpController.text.trim();

    if (!RegExp(r'^\d{6}$').hasMatch(code)) {
      setState(() => _clientError = 'Mã xác thực gồm đúng 6 chữ số.');
      return;
    }

    setState(() => _clientError = null);
    final ok = await ref.read(forgotPasswordOtpViewModelProvider.notifier).verifyOtp(code);
    if (ok && mounted) {
      Navigator.of(context).push(
        MaterialPageRoute<void>(builder: (_) => const ForgotPasswordOtpCompleteScreen()),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(forgotPasswordOtpViewModelProvider);
    final message = _clientError ?? state.errorMessage;

    return Scaffold(
      appBar: AppBar(title: const Text('Xác thực số điện thoại')),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(24, 20, 24, 32),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                'Nhập mã 6 số vừa gửi tới ${state.phoneNumber ?? "số điện thoại của bạn"}.',
                style: const TextStyle(fontSize: 15, color: AppColors.muted, height: 1.5),
              ),
              const SizedBox(height: 28),

              const Text('MÃ XÁC THỰC',
                  style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, letterSpacing: 1.1, color: AppColors.navy)),
              const SizedBox(height: 8),
              TextField(
                controller: _otpController,
                keyboardType: TextInputType.number,
                maxLength: 6,
                enabled: !state.isSubmitting,
                onSubmitted: (_) => _submit(),
                decoration: const InputDecoration(
                  hintText: '000000',
                  prefixIcon: Icon(Icons.sms_outlined),
                  counterText: '',
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
                    : const Text('XÁC THỰC'),
              ),

              const SizedBox(height: 18),
              TextButton(
                onPressed: state.isSubmitting || state.phoneNumber == null
                    ? null
                    : () => ref
                        .read(forgotPasswordOtpViewModelProvider.notifier)
                        .requestOtp(state.phoneNumber!),
                child: const Text('Gửi lại mã'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
