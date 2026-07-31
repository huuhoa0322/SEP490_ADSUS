import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/theme/app_theme.dart';
import 'features/auth/presentation/views/auth_gate.dart';

void main() {
  // ProviderScope là gốc của Riverpod, phải bọc toàn bộ ứng dụng.
  runApp(const ProviderScope(child: AdsusApp()));
}

class AdsusApp extends StatelessWidget {
  const AdsusApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'ADSUS',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light,
      // AuthGate quyết định hiển thị màn đăng nhập, màn ép đổi mật khẩu, hay màn chính.
      home: const AuthGate(),
    );
  }
}
