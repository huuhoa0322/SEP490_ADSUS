import 'package:adsus_mobile/features/ai_chatbot/data/models/chat_message_model.dart';
import 'package:adsus_mobile/main.dart' show AdsusApp;
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hive_flutter/hive_flutter.dart';
import 'package:integration_test/integration_test.dart';

import 'support/backend_test_client.dart';

/// BF-01 Account Onboarding & Access Lifecycle — Mobile System Test (Report 5.3, Scenario
/// Tool: integration_test). Xem `.ai-context/testing_conventions/testing-convention.md`
/// §Level-Specific Source Documents, dòng "System Test — Functional (ST-FR)":
/// `ADSUS_Mobile/integration_test/` THUỘC System Test (Report 5.3), KHÔNG PHẢI Integration
/// Test (Report 5.2.2, mocktail + Dio mock ở tầng repository) — 2 tài liệu khác nhau,
/// không trộn lẫn nội dung của nhau.
///
/// 7 case dưới đây chạy THẬT 100%: build app thật trên điện thoại thật, dựng dữ liệu qua
/// Admin API thật (`support/backend_test_client.dart`, cùng DB test Supabase riêng mà
/// `ADSUS_BE.SystemTests` BF-01→11 đã dùng), lái UI thật, không mock Dio, không mock Firebase.
///
/// KHÔNG có ở đây (ghi rõ lý do, không âm thầm bỏ qua):
///   - Tự đăng ký tài khoản mới (UC-05) và Quên mật khẩu (UC-02): cả hai bắt buộc qua
///     Firebase Phone Auth thật (OTP). `google-services.json` đã có trong repo, nhưng
///     `Firebase.initializeApp()` CHƯA từng được gọi trong bất kỳ integration_test nào của
///     repo này (`auth_flow_test.dart` cũ né hẳn bước này) — lần đầu bật Firebase Auth thật
///     trong 1 bài E2E mang rủi ro riêng (SHA-1/Play Integrity trên thiết bị thật), nên để
///     thành 1 module riêng sau khi bộ 7 case này đã chạy ổn định, không gộp vào lần đầu
///     tiên của module Auth.
///   - Đăng nhập bằng vân tay: không tồn tại trong code thật (đã grep toàn bộ `lib/`, không
///     có kết quả "fingerprint"/"biometric") — khớp đúng ghi chú UC-02 của
///     testing-convention.md ("planned, not yet in scope"). 9 case Auth mô tả trong
///     Report-5.2.2 (ITC004-009) nói về vân tay thuộc 1 tài liệu khác (Report 5.2, mocktail)
///     và mô tả 1 tính năng chưa từng được implement thật — không thuộc phạm vi module này.
///
/// Lệnh chạy mẫu (điện thoại thật, KHÔNG phải emulator — dùng IP LAN thật của máy chạy
/// backend, không dùng 10.0.2.2):
/// ```
/// flutter test integration_test/bf01_account_lifecycle_test.dart \
///   --dart-define=API_BASE_URL=http://192.168.1.10:5036 -d <deviceId>
/// ```
void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  late Dio setupDio;
  late String adminToken;

  setUpAll(() async {
    setupDio = createSetupDio();
    adminToken = await loginAsAdmin(setupDio);

    // MainShell dựng cả 4 tab cùng lúc qua IndexedStack (kể cả tab Chat Bot, dù không active) —
    // AiChatbotScreen.initState() mở Hive ngay khi mount bất kỳ tab nào. Test này KHÔNG chạy
    // qua main() thật (né Firebase.initializeApp() thật, xem ghi chú đầu file), nên phải tự
    // làm đúng bước Hive.registerAdapter()/initFlutter() y hệt main() đã làm — thiếu bước này
    // mọi case chạm tới Trang chủ (MainShell) đều crash với HiveError thật trên thiết bị.
    Hive.registerAdapter(HiveChatRoleAdapter());
    Hive.registerAdapter(ChatMessageModelAdapter());
    await Hive.initFlutter();
  });

  Future<void> pumpApp(WidgetTester tester) async {
    await tester.pumpWidget(const ProviderScope(child: AdsusApp()));
    await tester.pumpAndSettle();
  }

  // Trang chủ vừa vào (NotificationBell tự gọi API đếm thông báo chưa đọc, card "Thuốc hôm
  // nay" tự tải lịch uống thuốc) đôi khi vẫn còn 1 request bay giữa lúc pumpAndSettle() ở
  // dưới coi là đã "yên" và lúc hàm testWidgets thật sự kết thúc. Trên THIẾT BỊ THẬT (khác
  // hẳn máy ảo/CI), LiveTestWidgetsFlutterBinding ném lỗi cứng ("_pendingFrame == null" /
  // "!inTest") nếu có 1 frame bị yêu cầu sau khi test đã coi là xong — và lỗi đó làm hỏng
  // luôn TOÀN BỘ các case chạy sau trong cùng file, không chỉ case đang chạy. Vét thêm 1
  // nhịp thật (không dùng tester.pump thuần — cần nhường CPU thật cho Future đang bay) rồi
  // pumpAndSettle() lại lần nữa trước khi rời khỏi bất kỳ màn nào có gọi mạng nền như vậy.
  Future<void> settleBackgroundWork(WidgetTester tester) async {
    await Future<void>.delayed(const Duration(seconds: 2));
    await tester.pump(const Duration(seconds: 1));
    await tester.pumpAndSettle();
  }

  // Bàn phím ảo trên thiết bị thật vẫn còn mở (kèm thanh gợi ý/toolbar chọn văn bản) ngay
  // sau enterText() — cái đó nằm ĐÈ lên đúng toạ độ nút bấm bên dưới, khiến tester.tap()
  // tính đúng toạ độ tâm nút nhưng cú chạm lại rơi trúng lớp bàn phím/toolbar, không tới
  // được nút thật ("derived an Offset that would not hit test on the specified widget").
  // Chỉ xảy ra trên thiết bị thật (bàn phím ảo thật), không phải lỗi của nút hay của layout.
  // Bỏ focus trước MỌI lần tap nút để đóng hẳn bàn phím trước khi chạm.
  Future<void> dismissKeyboard(WidgetTester tester) async {
    FocusManager.instance.primaryFocus?.unfocus();
    await tester.pumpAndSettle();
  }

  // Một khoảng chờ CỐ ĐỊNH (5 giây) sau khi bấm nút không đáng tin trên mạng thật: tốc độ
  // phản hồi của backend thật (đặc biệt lượt gọi ĐẦU TIÊN của cả file — JIT/kết nối lần đầu)
  // dao động, có lúc vượt quá 5 giây. Thay vì đoán 1 con số, chờ ĐỘNG tới khi
  // CircularProgressIndicator trong nút (dấu hiệu isLoading=true) biến mất hẳn, tối đa
  // [timeout] — đúng bản chất request đã xong, không phụ thuộc phỏng đoán thời gian.
  Future<void> waitForLoadingToFinish(
    WidgetTester tester, {
    Duration timeout = const Duration(seconds: 30),
  }) async {
    final deadline = DateTime.now().add(timeout);
    while (DateTime.now().isBefore(deadline) &&
        find.byType(CircularProgressIndicator).evaluate().isNotEmpty) {
      await tester.pump(const Duration(milliseconds: 300));
    }
    await tester.pumpAndSettle();
  }

  Future<void> signIn(WidgetTester tester, String phone, String password) async {
    final textFields = find.byType(TextField);
    expect(textFields, findsNWidgets(2));
    await tester.enterText(textFields.at(0), phone);
    await tester.enterText(textFields.at(1), password);
    await tester.pumpAndSettle();
    await dismissKeyboard(tester);
    await tester.tap(find.widgetWithText(ElevatedButton, 'ĐĂNG NHẬP'));
    // Đợi 1 nhịp cho isLoading=true render (spinner xuất hiện) trước khi bắt đầu chờ nó
    // biến mất — chờ ngay lập tức đôi khi "thấy" màn hình lúc spinner chưa kịp render.
    await tester.pump(const Duration(milliseconds: 200));
    await waitForLoadingToFinish(tester);
    await settleBackgroundWork(tester);
  }

  testWidgets(
    'STC001 — Đăng nhập Patient hợp lệ vào được Trang chủ',
    (tester) async {
      final account =
          await createReadyPatient(setupDio, adminToken, fullName: 'STC001 Patient');
      await pumpApp(tester);

      await signIn(tester, account.phoneNumber, account.password);

      expect(find.text('Xin chào,'), findsOneWidget);
    },
  );

  testWidgets(
    'STC002 — Tài khoản không phải Patient (Doctor) bị từ chối, ở lại màn đăng nhập',
    (tester) async {
      final doctor = await createAccount(setupDio, adminToken,
          fullName: 'STC002 Doctor', role: 'DOCTOR');
      await pumpApp(tester);

      await signIn(tester, doctor.phoneNumber, doctor.temporaryPassword);

      // AuthRepositoryImpl chặn TRƯỚC khi ghi token xuống máy — không vào được Trang chủ,
      // và đúng câu thông báo riêng cho sai vai trò (khác câu sai mật khẩu/tài khoản khoá).
      expect(find.text('Xin chào,'), findsNothing);
      expect(
        find.text(
          'Tài khoản này sử dụng giao diện web của ADSUS. '
          'Ứng dụng di động chỉ dành cho bệnh nhân.',
        ),
        findsOneWidget,
      );
    },
  );

  testWidgets(
    'STC003 — Sai mật khẩu bị từ chối với đúng thông báo chung (GB-06)',
    (tester) async {
      final account =
          await createReadyPatient(setupDio, adminToken, fullName: 'STC003 Patient');
      await pumpApp(tester);

      await signIn(tester, account.phoneNumber, 'SaiMatKhau@999');

      expect(find.text('Xin chào,'), findsNothing);
      expect(find.text('Số điện thoại hoặc mật khẩu không đúng.'), findsOneWidget);
    },
  );

  testWidgets(
    'STC004 — Tài khoản đã bị Admin vô hiệu hoá không đăng nhập được, cùng thông báo với sai mật khẩu',
    (tester) async {
      final created = await createAccount(setupDio, adminToken,
          fullName: 'STC004 Patient', role: 'PATIENT');
      await deactivateAccount(setupDio, adminToken, created.userId);
      await pumpApp(tester);

      await signIn(tester, created.phoneNumber, created.temporaryPassword);

      // GB-06: tài khoản vô hiệu hoá KHÔNG được phân biệt được với sai mật khẩu — cùng
      // đúng 1 câu thông báo, không tiết lộ lý do thật.
      expect(find.text('Xin chào,'), findsNothing);
      expect(find.text('Số điện thoại hoặc mật khẩu không đúng.'), findsOneWidget);
    },
  );

  testWidgets(
    'STC005 — Tài khoản dùng mật khẩu tạm bị ép qua màn Đổi mật khẩu trước, xong mới vào được Trang chủ',
    (tester) async {
      final created = await createAccount(setupDio, adminToken,
          fullName: 'STC005 Patient', role: 'PATIENT');
      await pumpApp(tester);

      await signIn(tester, created.phoneNumber, created.temporaryPassword);

      // UC-25: goRouterProvider chặn tại '/change-password', KHÔNG vào Trang chủ dù số điện
      // thoại/mật khẩu đúng.
      expect(find.text('Xin chào,'), findsNothing);
      expect(find.text('Đổi mật khẩu'), findsWidgets);

      // mustChange=true: chỉ 2 ô (Mật khẩu mới, Xác nhận) — không có "Mật khẩu hiện tại".
      final passwordFields = find.byType(TextField);
      expect(passwordFields, findsNWidgets(2));

      const newPassword = 'Patient@2026';
      await tester.enterText(passwordFields.at(0), newPassword);
      await tester.enterText(passwordFields.at(1), newPassword);
      await tester.pumpAndSettle();
      await dismissKeyboard(tester);

      await tester.tap(find.widgetWithText(ElevatedButton, 'ĐỔI MẬT KHẨU'));
      await tester.pump(const Duration(milliseconds: 200));
      await waitForLoadingToFinish(tester);
      await settleBackgroundWork(tester);

      // Đổi xong -> ChangePasswordViewModel gọi clearMustChangePassword() -> router tự
      // chuyển sang Trang chủ, không cần thao tác gì thêm.
      expect(find.text('Xin chào,'), findsOneWidget);
    },
  );

  testWidgets(
    'STC006 — Đăng xuất xoá sạch phiên, quay lại đúng màn đăng nhập',
    (tester) async {
      final account =
          await createReadyPatient(setupDio, adminToken, fullName: 'STC006 Patient');
      await pumpApp(tester);
      await signIn(tester, account.phoneNumber, account.password);
      expect(find.text('Xin chào,'), findsOneWidget);

      await tester.tap(find.text('Cá nhân'));
      // Tab Cá nhân hiện CircularProgressIndicator toàn màn trong lúc tải hồ sơ — chờ động
      // tới khi tải xong, cùng lý do tránh đoán 1 con số cố định như bên trên.
      await tester.pump(const Duration(milliseconds: 200));
      await waitForLoadingToFinish(tester);

      await tester.tap(find.byTooltip('Đăng xuất'));
      await tester.pumpAndSettle();

      // Quay lại đúng SCR-02 ('/sign-in' của goRouterProvider), 2 TextField trống.
      expect(find.text('Đăng nhập'), findsWidgets);
      final textFields = find.byType(TextField);
      expect(textFields, findsNWidgets(2));
    },
  );

  testWidgets(
    'STC007 — Sửa họ tên trong Hồ sơ cá nhân, lưu và đọc lại đúng dữ liệu từ máy chủ thật',
    (tester) async {
      final account = await createReadyPatient(setupDio, adminToken,
          fullName: 'STC007 Original Name');
      await pumpApp(tester);
      await signIn(tester, account.phoneNumber, account.password);

      await tester.tap(find.text('Cá nhân'));
      await tester.pump(const Duration(milliseconds: 200));
      await waitForLoadingToFinish(tester);

      // Thứ tự field trên ProfileScreen: Họ và tên (enabled) rồi mới tới Số điện thoại
      // (disabled, BR-02) rồi Email — field đầu tiên luôn là Họ và tên.
      final nameField = find.byType(TextField).first;
      await tester.tap(nameField);
      await tester.pumpAndSettle();
      await tester.enterText(nameField, 'STC007 Updated Name');
      await tester.pumpAndSettle();
      await dismissKeyboard(tester);

      await tester.tap(find.text('LƯU THAY ĐỔI'));
      await tester.pump(const Duration(milliseconds: 200));
      await waitForLoadingToFinish(tester);

      // ProfileViewModel.save() đọc lại hồ sơ từ máy chủ rồi mới báo thành công — khẳng định
      // dữ liệu đã thực sự ghi xuống DB thật, không chỉ cập nhật state cục bộ trên máy.
      expect(find.text('Đã lưu thay đổi.'), findsOneWidget);
    },
  );
}
