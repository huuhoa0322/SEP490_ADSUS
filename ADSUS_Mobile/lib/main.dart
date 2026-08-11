import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'core/theme/app_theme.dart';
import 'features/auth/presentation/views/auth_gate.dart';

void main() async {
  // Đảm bảo Flutter engine đã sẵn sàng trước khi gọi plugin channel (SharedPreferences).
  WidgetsFlutterBinding.ensureInitialized();

  // Warm-up SharedPreferences — UC-16 cần instance này để lưu cờ "đã sync vào lịch".
  // Gọi ở main() thay vì trong FutureProvider để lần đầu mở app không phải đợi disk I/O
  // trên main thread UI; kết quả được cache trong plugin và dùng lại ngay sau đó.
  await SharedPreferences.getInstance();

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
