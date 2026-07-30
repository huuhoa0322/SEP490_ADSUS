import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_theme.dart';
import '../viewmodels/auth_view_model.dart';
import 'widgets/message_banner.dart';

/// SCR-02 — màn hình đăng nhập trên Mobile (UC-01, và UC-02 nếu đã bật sinh trắc học).
///
/// Bệnh nhân dùng màn này. Admin và Bác sĩ đăng nhập trên web (SCR-01).
class SignInScreen extends ConsumerStatefulWidget {
  const SignInScreen({super.key});

  @override
  ConsumerState<SignInScreen> createState() => _SignInScreenState();
}

class _SignInScreenState extends ConsumerState<SignInScreen> {
  final _phoneController = TextEditingController();
  final _passwordController = TextEditingController();
  bool _obscurePassword = true;
  String? _clientError;

  @override
  void dispose() {
    _phoneController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final phone = _phoneController.text.trim();
    final password = _passwordController.text;

    if (phone.isEmpty || password.isEmpty) {
      setState(() => _clientError = 'Vui lòng nhập số điện thoại và mật khẩu.');
      return;
    }

    setState(() => _clientError = null);
    await ref.read(authViewModelProvider.notifier).signIn(phone, password);
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(authViewModelProvider);
    final message = _clientError ?? state.errorMessage;

    return Scaffold(
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(24, 40, 24, 32),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              _buildHeader(),
              const SizedBox(height: 40),

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
                enabled: !state.isLoading,
                decoration: const InputDecoration(
                  hintText: '0900000000',
                  prefixIcon: Icon(Icons.phone_outlined),
                ),
              ),
              const SizedBox(height: 20),

              const Text(
                'MẬT KHẨU',
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w600,
                  letterSpacing: 1.1,
                  color: AppColors.navy,
                ),
              ),
              const SizedBox(height: 8),
              TextField(
                controller: _passwordController,
                obscureText: _obscurePassword,
                enabled: !state.isLoading,
                onSubmitted: (_) => _submit(),
                decoration: InputDecoration(
                  hintText: '••••••••',
                  prefixIcon: const Icon(Icons.lock_outline),
                  suffixIcon: IconButton(
                    icon: Icon(_obscurePassword
                        ? Icons.visibility_outlined
                        : Icons.visibility_off_outlined),
                    onPressed: () =>
                        setState(() => _obscurePassword = !_obscurePassword),
                  ),
                ),
              ),

              if (message != null) ...[
                const SizedBox(height: 18),
                MessageBanner(message: message),
              ],

              const SizedBox(height: 26),
              ElevatedButton(
                onPressed: state.isLoading ? null : _submit,
                child: state.isLoading
                    ? const SizedBox(
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          color: Colors.white,
                        ),
                      )
                    : const Text('ĐĂNG NHẬP'),
              ),

              // UC-02: chỉ hiện khi máy có cảm biến VÀ đã đăng nhập bằng mật khẩu
              // ít nhất một lần trên máy này (BR-01).
              if (state.canUseBiometric) ...[
                const SizedBox(height: 14),
                OutlinedButton.icon(
                  onPressed: state.isLoading
                      ? null
                      : () => ref
                          .read(authViewModelProvider.notifier)
                          .signInWithBiometric(),
                  icon: const Icon(Icons.fingerprint, size: 22),
                  label: const Text('ĐĂNG NHẬP BẰNG VÂN TAY'),
                ),
              ],

              const SizedBox(height: 22),
              const Text(
                'Quên mật khẩu? Liên hệ phòng khám để được cấp lại.',
                textAlign: TextAlign.center,
                style: TextStyle(fontSize: 13, color: AppColors.muted),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildHeader() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Container(
              height: 48,
              width: 48,
              decoration: const BoxDecoration(
                color: AppColors.teal,
                shape: BoxShape.circle,
              ),
              child: const Icon(Icons.document_scanner_outlined,
                  color: Colors.white, size: 26),
            ),
            const SizedBox(width: 12),
            const Text(
              'ADSUS',
              style: TextStyle(
                fontSize: 24,
                fontWeight: FontWeight.bold,
                color: AppColors.navy,
                letterSpacing: -0.5,
              ),
            ),
          ],
        ),
        const SizedBox(height: 28),
        const Text(
          'Đăng nhập',
          style: TextStyle(
            fontSize: 30,
            fontWeight: FontWeight.bold,
            color: AppColors.navy,
            height: 1.15,
          ),
        ),
        const SizedBox(height: 8),
        const Text(
          'Sử dụng số điện thoại đã được cấp để truy cập hệ thống.',
          style: TextStyle(fontSize: 15, color: AppColors.muted, height: 1.5),
        ),
      ],
    );
  }
}
