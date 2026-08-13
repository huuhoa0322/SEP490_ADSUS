import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../viewmodels/auth_view_model.dart';
import 'change_password_screen.dart';
import 'sign_in_screen.dart';
import '../../../../shared/main_shell.dart';

/// Quyết định màn hình nào được hiển thị, dựa trên trạng thái phiên đăng nhập.
///
/// Thứ tự ưu tiên bám đúng UCS:
///   1. Chưa đăng nhập            -> SCR-02 màn đăng nhập
///   2. Bị ép đổi mật khẩu        -> SCR-05, CHẶN mọi màn khác cho tới khi đổi xong (UC-25)
///   3. Bình thường               -> màn hình chính
///
/// Lưu ý: đây KHÔNG phải lớp bảo vệ dữ liệu. Lớp bảo vệ thật nằm ở [Authorize] phía
/// backend; mọi endpoint đều tự kiểm tra chữ ký token. Cổng này chỉ để trải nghiệm mượt.
class AuthGate extends ConsumerWidget {
  const AuthGate({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(authViewModelProvider);

    if (!state.isSignedIn) return const SignInScreen();

    if (state.session!.mustChangePassword) return const ChangePasswordScreen();

    return const MainShell();
  }
}
