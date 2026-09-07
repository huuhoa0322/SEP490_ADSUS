import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:home_widget/home_widget.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:workmanager/workmanager.dart';

import 'core/router/app_router.dart';
import 'core/theme/app_theme.dart';
import 'features/notification/services/notification_service.dart';
import 'features/medication_reminder/data/services/widget_sync_service.dart';
import 'features/medication_reminder/presentation/providers/medication_tab_provider.dart';

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

  // ADSUS Medication Widget (T-3.1): Khởi tạo WorkManager background periodic sync.
  // isInDebugMode=true để WorkManager chạy được trong debug build.
  await Workmanager().initialize(adsusCallbackDispatcher);

  // ADSUS Medication Widget (T-3.1): Đăng ký periodic task — 15 phút.
  await HomeWidget.initiallyLaunchedFromHomeWidget();

  // ProviderScope là gốc của Riverpod, phải bọc toàn bộ ứng dụng.
  runApp(const ProviderScope(child: AdsusApp()));
}

class AdsusApp extends ConsumerWidget {
  const AdsusApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    // Change 1: Đăng ký MethodChannel handler để nhận "openMedicationTab"
    // từ MainActivity khi user tap widget.
    // Dùng static guard để tránh đăng ký nhiều lần khi widget rebuild.
    _registerWidgetOpenMedicationHandler(ref);

    return MaterialApp.router(
      title: 'ADSUS',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light,
      // goRouterProvider quyết định hiển thị màn đăng nhập, màn ép đổi mật khẩu, hay màn
      // chính — xem core/router/app_router.dart.
      routerConfig: ref.watch(goRouterProvider),
    );
  }

  static bool _widgetHandlerRegistered = false;

  static void _registerWidgetOpenMedicationHandler(WidgetRef ref) {
    if (_widgetHandlerRegistered) return;
    _widgetHandlerRegistered = true;
    // Dùng BasicMessageChannel với JSONMessageCodec — an toàn hơn setMessageHandler thuần.
    const channel = BasicMessageChannel<dynamic>(
        'com.adsus.adsus_mobile/deep_link', JSONMessageCodec());
    channel.setMessageHandler((message) async {
      if (message is Map && message['method'] == 'openMedicationTab') {
        ref.read(initialMedicationTabProvider.notifier).state = true;
      }
      return null;
    });
  }
}
