import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_theme.dart';
import '../viewmodels/register_view_model.dart';
import 'widgets/message_banner.dart';
import 'register_complete_screen.dart';

/// Tự đăng ký, bước 2/3 — nhập mã OTP vừa nhận qua SMS.
class RegisterOtpScreen extends ConsumerStatefulWidget {
  const RegisterOtpScreen({super.key});

  @override
  ConsumerState<RegisterOtpScreen> createState() => _RegisterOtpScreenState();
}

class _RegisterOtpScreenState extends ConsumerState<RegisterOtpScreen> {
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
    final ok = await ref.read(registerViewModelProvider.notifier).verifyOtp(code);
    if (ok && mounted) {
      Navigator.of(context).push(
        MaterialPageRoute<void>(builder: (_) => const RegisterCompleteScreen()),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(registerViewModelProvider);
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

              const Text(
                'MÃ XÁC THỰC',
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w600,
                  letterSpacing: 1.1,
                  color: AppColors.navy,
                ),
              ),
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
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                      )
                    : const Text('XÁC THỰC'),
              ),

              const SizedBox(height: 18),
              TextButton(
                onPressed: state.isSubmitting || state.phoneNumber == null
                    ? null
                    : () => ref
                        .read(registerViewModelProvider.notifier)
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
