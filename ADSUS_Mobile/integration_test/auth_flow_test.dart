import 'package:adsus_mobile/main.dart' show AdsusApp;
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';

/// UC-01 end-to-end: đăng nhập bằng số điện thoại/mật khẩu thật -> vào Trang chủ.
///
/// Chạy trên emulator/thiết bị thật, gọi THẬT tới backend (không mock Dio) — cần:
///   1. `ADSUS_BE` đang chạy, đúng địa chỉ truyền qua `--dart-define=API_BASE_URL=...`.
///   2. 1 tài khoản Patient có thật trong DB, số điện thoại/mật khẩu truyền qua
///      `--dart-define=TEST_PHONE_NUMBER=...` `--dart-define=TEST_PASSWORD=...`
///      (giá trị mặc định dưới đây chỉ là placeholder, gần như chắc chắn sai với DB thật —
///      luôn tự truyền tay 2 define này khi chạy).
///
/// Lệnh chạy mẫu:
/// ```
/// flutter test integration_test/auth_flow_test.dart \
///   --dart-define=API_BASE_URL=http://10.0.2.2:5036 \
///   --dart-define=TEST_PHONE_NUMBER=0900000000 \
///   --dart-define=TEST_PASSWORD=Test@123
/// ```
///
/// LƯU Ý MÔI TRƯỜNG (28/08/2026, P_MB10): `flutterfire configure` chưa từng chạy trong repo
/// này (chưa có `firebase_options.dart`/`google-services.json` thật — xem ghi chú đầu
/// `10.1_prompt_mobile_implementation_workflow_adsus_flutter.md`). Test này KHÔNG gọi
/// `Firebase.initializeApp()` (khác `main()` thật) để né phụ thuộc đó — luồng đăng nhập
/// (`AuthRepositoryImpl.signIn()`) đã tự bọc try/catch quanh việc đăng ký FCM token, nên
/// không cần Firebase để đăng nhập thành công. Nếu `NotificationBell` hay widget nào khác
/// trong cây `HomeScreen` lỡ đọc thẳng 1 provider Firebase chưa init (không qua try/catch),
/// test này sẽ crash ngay khi build tới đó — đó là phát hiện thật cần sửa, không phải lỗi
/// của bài test.
void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  const testPhoneNumber =
      String.fromEnvironment('TEST_PHONE_NUMBER', defaultValue: '0900000000');
  const testPassword = String.fromEnvironment('TEST_PASSWORD', defaultValue: 'Test@123');

  testWidgets(
    'signIn_ValidPatientCredentials_NavigatesToHomeScreen',
    (tester) async {
      await tester.pumpWidget(const ProviderScope(child: AdsusApp()));
      await tester.pumpAndSettle();

      // SCR-02 — đúng 2 TextField theo thứ tự trong cây widget: số điện thoại rồi mật khẩu
      // (xem sign_in_screen.dart).
      final textFields = find.byType(TextField);
      expect(textFields, findsNWidgets(2));

      await tester.enterText(textFields.at(0), testPhoneNumber);
      await tester.enterText(textFields.at(1), testPassword);
      await tester.pumpAndSettle();

      await tester.tap(find.widgetWithText(ElevatedButton, 'ĐĂNG NHẬP'));

      // Đợi network thật + go_router redirect chạy xong. KHÔNG dùng pumpAndSettle() đơn
      // thuần ngay sau tap: nút có CircularProgressIndicator animation lặp vô hạn trong lúc
      // đợi request, pumpAndSettle() sẽ time out chờ animation dừng dù request đã xong từ lâu.
      await tester.pump(const Duration(seconds: 5));
      await tester.pumpAndSettle();

      // Trang chủ hiện "Xin chào," ngay khi vào (xem home_screen.dart) — dấu hiệu rõ nhất
      // là đã rời khỏi màn đăng nhập và go_router đã tự chuyển hướng theo AuthState mới.
      expect(find.text('Xin chào,'), findsOneWidget);
    },
  );
}
