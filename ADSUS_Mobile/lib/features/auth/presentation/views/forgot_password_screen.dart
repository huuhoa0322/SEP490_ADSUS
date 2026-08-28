import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/utils/phone_number_rule.dart';
import '../../../../core/theme/app_theme.dart';
import '../viewmodels/forgot_password_view_model.dart';
import 'widgets/message_banner.dart';

/// UC-03 FT-06 — người dùng tự yêu cầu cấp lại mật khẩu, mở từ SCR-02.
///
/// Màn này chưa có SCR-ID riêng trong Screen List của PRD; UCS đã ghi nhận đó là khoảng
/// trống và để lại cho FDS, nên không tự đặt mã mới.
///
/// Điểm quan trọng: dù nhập đúng hay sai, người dùng LUÔN nhận đúng một câu trả lời (AF-01).
/// Báo "số điện thoại không tồn tại" là biến màn này thành công cụ dò tài khoản.
class ForgotPasswordScreen extends ConsumerStatefulWidget {
  const ForgotPasswordScreen({super.key});

  @override
  ConsumerState<ForgotPasswordScreen> createState() => _ForgotPasswordScreenState();
}

class _ForgotPasswordScreenState extends ConsumerState<ForgotPasswordScreen> {
  final _phoneController = TextEditingController();
  final _emailController = TextEditingController();

  String? _clientError;

  @override
  void dispose() {
    _phoneController.dispose();
    _emailController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final phone = _phoneController.text.trim();
    final email = _emailController.text.trim();

    if (phone.isEmpty || email.isEmpty) {
      setState(() => _clientError = 'Vui lòng nhập số điện thoại và email đã đăng ký.');
      return;
    }

    // Kiểm định dạng KHÔNG phạm AF-01: câu báo lỗi chỉ nói "chuỗi này không thể là số điện
    // thoại", đúng với mọi giá trị sai dạng, chứ không hé lộ số đó có tài khoản hay không.
    // Thiếu bước này thì gõ nhầm một chữ số vẫn nhận được câu "đã gửi yêu cầu", rồi ngồi chờ
    // mail mãi không tới mà tưởng hệ thống hỏng.
    if (!PhoneNumberRule.isValid(phone)) {
      setState(() => _clientError = PhoneNumberRule.errorMessage);
      return;
    }

    setState(() => _clientError = null);

    await ref
        .read(forgotPasswordViewModelProvider.notifier)
        .submit(phoneNumber: phone, email: email);
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(forgotPasswordViewModelProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Quên mật khẩu')),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(24, 20, 24, 32),
          child: state.sent ? _buildSentMessage() : _buildForm(state),
        ),
      ),
    );
  }

  Widget _buildForm(ForgotPasswordState state) {
    final errorMessage = _clientError ?? state.errorMessage;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text(
          'Nhập số điện thoại và email đã đăng ký. Hệ thống sẽ gửi mật khẩu mới tới email đó.',
          style: TextStyle(fontSize: 15, color: AppColors.muted, height: 1.5),
        ),
        const SizedBox(height: 28),

        _label('SỐ ĐIỆN THOẠI'),
        TextField(
          controller: _phoneController,
          keyboardType: TextInputType.phone,
          enabled: !state.isSending,
          decoration: const InputDecoration(
            hintText: '0900000000',
            prefixIcon: Icon(Icons.phone_outlined),
          ),
        ),
        const SizedBox(height: 20),

        _label('EMAIL ĐÃ ĐĂNG KÝ'),
        TextField(
          controller: _emailController,
          keyboardType: TextInputType.emailAddress,
          enabled: !state.isSending,
          onSubmitted: (_) => _submit(),
          decoration: const InputDecoration(
            hintText: 'email@example.com',
            prefixIcon: Icon(Icons.mail_outline),
          ),
        ),

        if (errorMessage != null) ...[
          const SizedBox(height: 18),
          MessageBanner(message: errorMessage),
        ],

        const SizedBox(height: 26),
        ElevatedButton(
          onPressed: state.isSending ? null : _submit,
          child: state.isSending
              ? const SizedBox(
                  height: 20,
                  width: 20,
                  child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                )
              : const Text('GỬI MẬT KHẨU MỚI'),
        ),

        const SizedBox(height: 22),
        const Text(
          'Không nhớ email đã đăng ký? Liên hệ phòng khám để được cấp lại.',
          textAlign: TextAlign.center,
          style: TextStyle(fontSize: 13, color: AppColors.muted),
        ),
      ],
    );
  }

  Widget _buildSentMessage() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const SizedBox(height: 12),
        const Icon(Icons.mark_email_read_outlined, size: 56, color: AppColors.teal),
        const SizedBox(height: 20),
        const Text(
          'Đã gửi yêu cầu',
          textAlign: TextAlign.center,
          style: TextStyle(
            fontSize: 24,
            fontWeight: FontWeight.bold,
            color: AppColors.navy,
          ),
        ),
        const SizedBox(height: 14),

        // Câu này cố tình mơ hồ — xem chú thích ở đầu tệp (AF-01).
        const Text(
          'Nếu thông tin bạn nhập khớp với một tài khoản, hệ thống đã gửi mật khẩu mới tới '
          'email đó. Vui lòng kiểm tra hộp thư, kể cả mục spam.',
          textAlign: TextAlign.center,
          style: TextStyle(fontSize: 15, color: AppColors.muted, height: 1.5),
        ),
        const SizedBox(height: 12),
        const Text(
          'Đăng nhập bằng mật khẩu mới xong, ứng dụng sẽ yêu cầu bạn đặt lại mật khẩu riêng.',
          textAlign: TextAlign.center,
          style: TextStyle(fontSize: 15, color: AppColors.muted, height: 1.5),
        ),

        const SizedBox(height: 30),
        ElevatedButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('VỀ TRANG ĐĂNG NHẬP'),
        ),
      ],
    );
  }

  Widget _label(String text) => Padding(
        padding: const EdgeInsets.only(bottom: 8),
        child: Text(
          text,
          style: const TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w600,
            letterSpacing: 1.1,
            color: AppColors.navy,
          ),
        ),
      );
}
