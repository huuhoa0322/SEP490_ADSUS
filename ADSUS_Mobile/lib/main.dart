import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'core/router/app_router.dart';
import 'core/theme/app_theme.dart';
import 'features/notification/services/notification_service.dart';

void main() async {
  // Đảm bảo Flutter engine đã sẵn sàng trước khi gọi plugin channel.
  WidgetsFlutterBinding.ensureInitialized();

  // 1. Khởi tạo Firebase — bắt buộc phải trước khi dùng bất kỳ Firebase service nào.
  await Firebase.initializeApp();

  // 2. Khởi tạo NotificationService — setup FCM listeners.
  // Phải gọi SAU Firebase.initializeApp() vì service dùng FirebaseMessaging.
  await notificationService.initialize();

  // 3. Warm-up SharedPreferences — UC-16 cần instance này để lưu cờ "đã sync vào lịch".
  // Gọi ở main() thay vì trong FutureProvider để lần đầu mở app không phải đợi disk I/O
  // trên main thread UI; kết quả được cache trong plugin và dùng lại ngay sau đó.
  await SharedPreferences.getInstance();

  // ProviderScope là gốc của Riverpod, phải bọc toàn bộ ứng dụng.
  runApp(const ProviderScope(child: AdsusApp()));
}

class AdsusApp extends ConsumerWidget {
  const AdsusApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return MaterialApp.router(
      title: 'ADSUS',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light,
      // goRouterProvider quyết định hiển thị màn đăng nhập, màn ép đổi mật khẩu, hay màn
      // chính — xem core/router/app_router.dart.
      routerConfig: ref.watch(goRouterProvider),
    );
  }
}
