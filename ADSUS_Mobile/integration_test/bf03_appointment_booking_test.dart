import 'package:adsus_mobile/features/ai_chatbot/data/models/chat_message_model.dart';
import 'package:adsus_mobile/features/appointment_scheduling/presentation/viewmodels/book_appointment_view_model.dart'
    show DoctorOption;
import 'package:adsus_mobile/features/appointment_scheduling/presentation/views/widgets/appointment_card.dart';
import 'package:adsus_mobile/main.dart' show AdsusApp;
import 'package:dio/dio.dart';
import 'package:dropdown_search/dropdown_search.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hive_flutter/hive_flutter.dart';
import 'package:integration_test/integration_test.dart';

import 'support/backend_test_client.dart';

/// BF-03 Appointment Booking & Clinic Reception — Mobile System Test (Report 5.3, Scenario
/// Tool: integration_test). Cùng quy ước với `bf01_account_lifecycle_test.dart`: đây thuộc
/// System Test (Report 5.3-Mobile), không phải Integration Test (Report 5.2.2).
///
/// Phạm vi Mobile thật của BF-03 (đọc từ `2. Main Business Flow.md` §2.3, chỉ những bước
/// Patient tự làm trên app di động):
///   - Bước 5: Patient đặt 1 slot Open cho chính mình hoặc người thân sở hữu.
///   - Bước 10: Patient xem lịch hẹn của mình và có thể hủy kèm lý do.
/// Không thuộc phạm vi Mobile (Doctor/Nurse/Staff/Admin-only, đã có ở Report 5.3 gốc/web):
/// sinh slot mặc định (JOB-02), Doctor quản lý slot, duyệt nghỉ/tăng ca, Nurse đặt hộ tại
/// quầy, check-in, NO_SHOW.
///
/// KHÔNG có ở đây (ghi rõ lý do):
///   - UC-18 (đồng bộ lịch vào Calendar thiết bị): mở dialog CALENDAR NATIVE của hệ điều
///     hành (package `device_calendar`), nằm ngoài cây widget Flutter — không có cách nào
///     xác nhận qua `integration_test` (không đọc được nội dung dialog native, không biết
///     người dùng thật sẽ bấm gì). Cùng loại giới hạn với OTP Firebase ở BF-01.
///   - Luật hủy trong vòng 12 giờ trước giờ khám (BR-057): đã có bài kiểm tra thật, kỹ ở
///     tầng backend (`ADSUS_BE.SystemTests/BF03_AppointmentBooking`) — lặp lại ở đây chỉ
///     kiểm tra thêm "app di động có hiển thị đúng lỗi backend trả về không", trong khi lại
///     mang đúng rủi ro giờ-chạy-phụ-thuộc-giờ-thật (flaky) mà bên backend từng gặp và sửa.
///     Để dành cho 1 module riêng nếu cần, không gộp vào đây.
///
/// Lệnh chạy mẫu (điện thoại thật):
/// ```
/// flutter test integration_test/bf03_appointment_booking_test.dart --dart-define-from-file=.env
/// ```
void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  late Dio setupDio;
  late String adminToken;

  setUpAll(() async {
    setupDio = createSetupDio();
    adminToken = await loginAsAdmin(setupDio);

    // MainShell dựng cả 4 tab qua IndexedStack ngay khi vào Trang chủ — AiChatbotScreen mở
    // Hive ngay lúc mount. Test này né main() thật (né Firebase.initializeApp() chưa kiểm
    // chứng), nên phải tự làm bước Hive y hệt main() — xem cùng lý do ở bf01_*.
    Hive.registerAdapter(HiveChatRoleAdapter());
    Hive.registerAdapter(ChatMessageModelAdapter());
    await Hive.initFlutter();
  });

  Future<void> pumpApp(WidgetTester tester) async {
    await tester.pumpWidget(const ProviderScope(child: AdsusApp()));
    await tester.pumpAndSettle();
  }

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

  Future<void> settleBackgroundWork(WidgetTester tester) async {
    await Future<void>.delayed(const Duration(seconds: 2));
    await tester.pump(const Duration(seconds: 1));
    await tester.pumpAndSettle();
  }

  Future<void> dismissKeyboard(WidgetTester tester) async {
    FocusManager.instance.primaryFocus?.unfocus();
    await tester.pumpAndSettle();
  }

  // Dữ liệu test tích luỹ dần qua các lần chạy trước (không tự dọn dẹp — quyết định đã chốt
  // từ backend System Test), nên nếu dùng tên bác sĩ CỐ ĐỊNH kiểu "STC001-03 Doctor", lần
  // chạy thứ 2 trở đi DB sẽ có NHIỀU bác sĩ trùng tên — tìm kiếm rồi tap theo tên có thể
  // chọn NHẦM 1 bác sĩ cũ (không có slot cho ngày đang test), khiến "08:00" không bao giờ
  // xuất hiện dù toàn bộ các bước trước đó "thành công". Luôn gắn thêm hậu tố thời gian thật
  // để tên bác sĩ/người thân của MỖI LẦN CHẠY là duy nhất trong toàn bộ lịch sử DB test.
  String uniqueSuffix() => DateTime.now().millisecondsSinceEpoch.toString().substring(7);

  Future<void> signIn(WidgetTester tester, String phone, String password) async {
    final textFields = find.byType(TextField);
    expect(textFields, findsNWidgets(2));
    await tester.enterText(textFields.at(0), phone);
    await tester.enterText(textFields.at(1), password);
    await tester.pumpAndSettle();
    await dismissKeyboard(tester);
    await tester.tap(find.widgetWithText(ElevatedButton, 'ĐĂNG NHẬP'));
    await tester.pump(const Duration(milliseconds: 200));
    await waitForLoadingToFinish(tester);

    // Kiểm tra NGAY sau khi request đăng nhập xong, TRƯỚC settleBackgroundWork() — để phân
    // biệt 2 khả năng hoàn toàn khác nhau: (a) đăng nhập chưa từng thành công (còn ở Sign In
    // ngay từ đây), hay (b) đăng nhập ĐÃ THÀNH CÔNG, vào Trang chủ, rồi bị 1 interceptor Dio
    // toàn cục (onSessionExpired, xem app_providers.dart) bật ngược lại Sign In sau đó vì
    // MỘT lệnh gọi nền khác (đếm thông báo, đồng bộ widget, tải lịch sử chat...) trả về 401.
    // Nếu KHÔNG chặn ở đây, ca (b) sẽ bị hiểu lầm thành "chưa từng đăng nhập được".
    final reachedHomeInitially = find.text('Xin chào,').evaluate().isNotEmpty;

    await settleBackgroundWork(tester);

    if (find.text('Đăng nhập').evaluate().isNotEmpty) {
      final bannerText = tester
          .widgetList<Text>(find.byType(Text))
          .map((t) => t.data)
          .whereType<String>()
          .where((s) => s.isNotEmpty)
          .join(' | ');
      if (reachedHomeInitially) {
        fail(
          'Đăng nhập THÀNH CÔNG (đã thấy Trang chủ) nhưng bị bật ngược lại màn Đăng nhập ngay '
          'sau đó — dấu hiệu rõ của interceptor onSessionExpired (app_providers.dart) phản ứng '
          'với 1 request 401 từ lệnh gọi NỀN nào đó ngay sau khi vào Trang chủ (đếm thông báo / '
          'đồng bộ widget / tải lịch sử chat...), KHÔNG PHẢI do sai thông tin đăng nhập hay rate '
          'limit. Nội dung màn hình lúc này: $bannerText',
        );
      }
      fail(
        'Vẫn còn ở màn Đăng nhập sau khi bấm ĐĂNG NHẬP, CHƯA từng thấy Trang chủ — có thể đã '
        'chạm rate limit đăng nhập (10 request/phút/IP, xem RateLimitPolicies.Auth) do file '
        'test này gọi login nhiều lần trong 1 lượt chạy, cộng dồn với các lần chạy lại gần đây. '
        'Nội dung màn hình lúc này: $bannerText',
      );
    }
  }

  // STC001 là test ĐẦU TIÊN trong file — lần pumpWidget() đầu tiên của cả tiến trình phải
  // trả giá khởi động thật (dựng cây provider Riverpod lần đầu, JIT...), y hệt lý do lượt
  // gọi mạng đầu tiên phải "làm nóng" ở NFR Performance test phía backend. settleBackgroundWork
  // (3 giây cố định) có thể không đủ cho đúng LẦN ĐẦU này. Chờ ĐỘNG tới khi text xuất hiện,
  // thay vì đoán 1 khoảng cố định, để không phải tăng số cho tất cả các test còn lại.
  Future<void> waitForText(
    WidgetTester tester,
    String text, {
    Duration timeout = const Duration(seconds: 15),
  }) async {
    final deadline = DateTime.now().add(timeout);
    while (DateTime.now().isBefore(deadline) && find.text(text).evaluate().isEmpty) {
      await tester.pump(const Duration(milliseconds: 300));
    }
    await tester.pumpAndSettle();
  }

  // ---- Tính ngày/tuần đặt lịch — cùng thuật toán book_appointment_screen.dart ----
  //
  // Đặt CÁCH HÔM NAY ÍT NHẤT 2 NGÀY, không phải 1 ngày: BF-01 seed slot buổi sáng sớm nhất
  // (08:00) — nếu chỉ đặt 1 ngày trước, slot 08:00 đó có thể rơi vào trong mốc 12 giờ trước
  // giờ khám (BR-057) tùy giờ chạy test, y hệt lỗi flaky STC005 đã gặp và sửa ở
  // ADSUS_BE.SystemTests/BF03_AppointmentBooking. 2 ngày là đủ an toàn bất kể giờ chạy.
  DateTime dateOnly(DateTime d) => DateTime(d.year, d.month, d.day);

  DateTime pickBookableDate() => dateOnly(DateTime.now()).add(const Duration(days: 2));

  int weekIndexFor(DateTime target) {
    final now = DateTime.now();
    final currentMonday = dateOnly(now).subtract(Duration(days: now.weekday - 1));
    return target.difference(currentMonday).inDays ~/ 7;
  }

  String weekChipLabel(int weekIndex) {
    final now = DateTime.now();
    final currentMonday = dateOnly(now).subtract(Duration(days: now.weekday - 1));
    final weekMonday = currentMonday.add(Duration(days: weekIndex * 7));
    final weekSunday = weekMonday.add(const Duration(days: 6));
    String pad2(int n) => n.toString().padLeft(2, '0');
    return 'Tuần (${pad2(weekMonday.day)}/${pad2(weekMonday.month)} - '
        '${pad2(weekSunday.day)}/${pad2(weekSunday.month)})';
  }

  String dateChipLabel(DateTime d) {
    const weekdays = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];
    final wd = weekdays[d.weekday - 1];
    return '$wd (${d.day.toString().padLeft(2, '0')}/${d.month.toString().padLeft(2, '0')})';
  }

  // Slot buổi sáng sớm nhất do ensure-default sinh ra luôn là 08:00 (xem cùng ghi chú đã
  // xác nhận ở ADSUS_BE.SystemTests: "CreateSlotAsync always picks the earliest (08:00)
  // Open slot for a date").
  const morningSlotLabel = '08:00';

  // Toàn bộ màn Đặt lịch nằm trong 1 SingleChildScrollView — khi phần "Đặt lịch cho" mở
  // rộng (chế độ Người thân, có dropdown + nút thêm người thân), các phần bên dưới (Chọn
  // bác sĩ, ngày, slot, nút xác nhận) bị đẩy xuống NGOÀI viewport đang hiển thị. find.text()
  // vẫn định vị được widget trong cây (SingleChildScrollView dựng sẵn toàn bộ con, không lazy
  // như ListView), nhưng tap() tại toạ độ đó có thể rơi ra ngoài vùng thật sự hiển thị/nhận
  // được chạm — đúng nguyên nhân các lần fail "derived an Offset that would not hit test" và
  // "Offset ... is outside the bounds of the root of the render tree" trước đó. Luôn
  // ensureVisible() (tự cuộn Scrollable gần nhất) ngay trước khi chạm bất kỳ phần tử nào có
  // thể nằm ngoài màn hình theo cách này.
  Future<void> tapVisible(WidgetTester tester, Finder finder) async {
    await tester.ensureVisible(finder);
    await tester.pumpAndSettle();
    await tester.tap(finder);
    await tester.pumpAndSettle();
  }

  /// Chọn Bác sĩ (qua ô tìm kiếm DropdownSearch) -> chọn tuần (nếu khác tuần này) -> chọn
  /// ngày -> chọn slot 08:00. Dùng chung cho STC001/002/004 — 3 case đều cần 1 slot đã
  /// chọn để nút "XÁC NHẬN ĐẶT LỊCH" bật lên, khác nhau ở bước sau đó.
  Future<void> pickDoctorDateAndSlot(
    WidgetTester tester,
    String doctorFullName,
    DateTime targetDate,
  ) async {
    // Tap thẳng vào Text "Chọn bác sĩ" liên tục bị cảnh báo "would not hit test" trên thiết
    // bị thật dù đã ensureVisible() — vùng bao của riêng chữ đó (RenderParagraph) hẹp và lệch
    // trái so với icon + padding của cả field, trong khi InkWell thật (đọc source
    // dropdown_search: build() bọc InkWell quanh TOÀN BỘ _formField()) bao trùm rộng hơn
    // nhiều. Tap thẳng vào WIDGET DropdownSearch (vùng bao lớn, đúng cả field) thay vì vào
    // đúng chữ bên trong nó, để chắc chắn rơi vào vùng InkWell thật.
    await tapVisible(tester, find.byType(DropdownSearch<DoctorOption>));

    final searchField = find.byWidgetPredicate(
      (w) => w is TextField && w.decoration?.hintText == 'Tìm kiếm bác sĩ...',
    );
    expect(searchField, findsOneWidget);
    await tester.enterText(searchField, doctorFullName);
    await tester.pumpAndSettle();
    await dismissKeyboard(tester);

    // dropdown_search (package đang dùng, v5.0.6) debounce đúng 1 giây thật
    // (PopupProps.searchDelay mặc định) trước khi bộ lọc tìm kiếm thật sự chạy — timer đó
    // không tính là "còn frame đang chờ" nên pumpAndSettle() đơn thuần không chờ đủ. Không
    // chờ hết debounce thì popup vẫn đang hiện DANH SÁCH CHƯA LỌC (hàng chục nghìn bác sĩ
    // tích luỹ qua các lần test) — tap theo tên lúc đó gần như chắc chắn trật, và khớp đúng
    // hiện tượng "Chọn bác sĩ" không hề đổi dù tap không ném lỗi gì.
    await Future<void>.delayed(const Duration(milliseconds: 1200));
    await tester.pumpAndSettle();

    await waitForText(tester, doctorFullName);
    // Popup DropdownSearch tự cuộn RIÊNG (constraints: maxHeight 300, khác hẳn cuộn của
    // trang chính) — find.text() vẫn định vị được item dù nó đang nằm ngoài vùng đang cuộn
    // của CHÍNH popup đó. Thiếu ensureVisible() ở đúng cú tap này là chỗ tôi đã bỏ sót —
    // tapVisible() trước đó chỉ áp cho tap mở field, không áp cho tap chọn item bên trong.
    final doctorItem = find.text(doctorFullName).last;
    await tester.ensureVisible(doctorItem);
    await tester.pumpAndSettle();
    await tester.tap(doctorItem);
    // Popup DropdownSearch đóng lại bằng animation mờ dần (AnimatedOpacity) — pumpAndSettle()
    // đơn thuần đôi khi vẫn còn bắt trúng lớp overlay đang mờ dần đó ở cú tap kế tiếp
    // ("derived an Offset that would not hit test..."), cùng lớp lỗi với bàn phím ảo ở BF-01.
    await tester.pumpAndSettle();
    await settleBackgroundWork(tester);

    // Điểm kiểm tra 1/2: nếu tap ở trên KHÔNG thật sự chọn được bác sĩ (chỉ trúng 1 gesture
    // detector khác trong lúc bị cảnh báo "would not hit test"), placeholder "Chọn bác sĩ"
    // vẫn còn đó. Khẳng định ngay ở đây để lần fail SAU (nếu có) chỉ đúng vào bước còn lại,
    // thay vì lại phải đoán lại từ đầu "08:00" không thấy là do đâu.
    expect(
      find.text('Chọn bác sĩ'),
      findsNothing,
      reason: 'Bác sĩ "$doctorFullName" chưa thật sự được chọn — placeholder vẫn còn hiện.',
    );

    final weekIndex = weekIndexFor(targetDate);
    if (weekIndex != 0) {
      await tapVisible(tester, find.text(weekChipLabel(weekIndex)));
    }

    await tapVisible(tester, find.text(dateChipLabel(targetDate)));

    // Điểm kiểm tra 2/2: bác sĩ và ngày đều đã chọn — nếu app tự báo "Không có khung giờ cho
    // ngày đã chọn." thì đây là vấn đề DỮ LIỆU thật (slot không tồn tại/không khớp lọc phía
    // server), khác hẳn với vấn đề THAO TÁC (tap trượt/cuộn) — 2 nguyên nhân cần 2 hướng sửa
    // hoàn toàn khác nhau.
    expect(
      find.text('Không có khung giờ cho ngày đã chọn.'),
      findsNothing,
      reason: 'App tự báo hết slot cho bác sĩ "$doctorFullName" ngày ${dateChipLabel(targetDate)} '
          '— slot đã tạo qua ensure-default (ensureMorningSlotId) không khớp với những gì màn '
          'Đặt lịch đang lọc ra, dù ensureMorningSlotId() đã tạo thành công (không ném lỗi lúc '
          'dựng dữ liệu). Cần xem lại logic lọc theo doctorId/ngày ở BookAppointmentState, '
          'không phải vấn đề thao tác chạm/cuộn.',
    );

    await tapVisible(tester, find.text(morningSlotLabel));
  }

  testWidgets(
    'STC001 — Patient tự đặt lịch cho chính mình, xuất hiện đúng ở tab Lịch của tôi',
    (tester) async {
      final targetDate = pickBookableDate();
      final doctorName = 'STC001-03 Doctor ${uniqueSuffix()}';
      final doctor = await createReadyDoctor(setupDio, adminToken, fullName: doctorName);
      await ensureMorningSlotId(setupDio, doctor.accessToken, targetDate);
      final patient = await createBookablePatient(
        setupDio,
        adminToken,
        doctor.accessToken,
        fullName: 'STC001-03 Patient',
      );

      await pumpApp(tester);
      await signIn(tester, patient.phoneNumber, patient.password);

      await waitForText(tester, 'Đặt lịch khám');
      await tester.ensureVisible(find.text('Đặt lịch khám'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Đặt lịch khám'));
      await tester.pump(const Duration(milliseconds: 200));
      await waitForLoadingToFinish(tester);

      await pickDoctorDateAndSlot(tester, doctorName, targetDate);

      await tester.ensureVisible(find.widgetWithText(ElevatedButton, 'XÁC NHẬN ĐẶT LỊCH'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(ElevatedButton, 'XÁC NHẬN ĐẶT LỊCH'));
      await tester.pump(const Duration(milliseconds: 200));
      await waitForLoadingToFinish(tester);
      await settleBackgroundWork(tester);

      // Đặt xong -> tự điều hướng sang MyAppointmentsScreen ("Lịch khám của tôi").
      expect(find.text('Lịch khám của tôi'), findsOneWidget);
      expect(find.text('BS. $doctorName'), findsOneWidget);
    },
  );

  testWidgets(
    'STC002 — Patient đặt lịch hộ người thân, hiện đúng badge "Đặt hộ" ở tab Lịch người thân',
    (tester) async {
      final targetDate = pickBookableDate();
      final doctorName = 'STC002-03 Doctor ${uniqueSuffix()}';
      final relativeName = 'STC002-03 Relative ${uniqueSuffix()}';
      final doctor = await createReadyDoctor(setupDio, adminToken, fullName: doctorName);
      await ensureMorningSlotId(setupDio, doctor.accessToken, targetDate);
      final patient = await createBookablePatient(
        setupDio,
        adminToken,
        doctor.accessToken,
        fullName: 'STC002-03 Patient',
      );
      final patientToken = await loginRaw(setupDio, patient.phoneNumber, patient.password);
      await createRelative(
        setupDio,
        patientToken,
        fullName: relativeName,
        relationshipName: 'Con',
      );

      await pumpApp(tester);
      await signIn(tester, patient.phoneNumber, patient.password);

      await waitForText(tester, 'Đặt lịch khám');
      await tester.ensureVisible(find.text('Đặt lịch khám'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Đặt lịch khám'));
      await tester.pump(const Duration(milliseconds: 200));
      await waitForLoadingToFinish(tester);

      // Chuyển sang "Người thân" TRƯỚC — chờ danh sách người thân tải xong trước khi chọn
      // bác sĩ/ngày/slot, tránh việc mở popup bác sĩ đúng lúc danh sách người thân đang tải.
      await tapVisible(tester, find.text('Người thân'));
      await waitForLoadingToFinish(tester);

      await tapVisible(tester, find.text('Chọn người thân'));
      await tester.tap(find.text('Con $relativeName').last);
      // Cùng lý do settle sau khi đóng popup DropdownSearch bác sĩ — dropdown chọn người
      // thân cũng đóng bằng animation, cần vét thêm trước khi mở popup bác sĩ kế tiếp.
      await tester.pumpAndSettle();
      await settleBackgroundWork(tester);

      await pickDoctorDateAndSlot(tester, doctorName, targetDate);

      await tester.ensureVisible(find.widgetWithText(ElevatedButton, 'XÁC NHẬN ĐẶT LỊCH'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(ElevatedButton, 'XÁC NHẬN ĐẶT LỊCH'));
      await tester.pump(const Duration(milliseconds: 200));
      await waitForLoadingToFinish(tester);
      await settleBackgroundWork(tester);

      expect(find.text('Lịch khám của tôi'), findsOneWidget);

      // Mặc định tab hiện ra là "Lịch của tôi" — chuyển sang tab người thân để thấy cuộc hẹn
      // vừa đặt hộ, kèm badge "Đặt hộ".
      await tapVisible(tester, find.textContaining('Lịch người thân'));

      expect(find.textContaining('Đặt hộ:'), findsOneWidget);
      expect(find.text(relativeName), findsOneWidget);
      expect(find.text('BS. $doctorName'), findsOneWidget);
    },
  );

  testWidgets(
    'STC003 — Patient hủy lịch khám đã đặt kèm lý do, badge chuyển sang Đã hủy',
    (tester) async {
      final targetDate = pickBookableDate();
      final doctorName = 'STC003-03 Doctor ${uniqueSuffix()}';
      final doctor = await createReadyDoctor(setupDio, adminToken, fullName: doctorName);
      final slotId = await ensureMorningSlotId(setupDio, doctor.accessToken, targetDate);
      final patient = await createBookablePatient(
        setupDio,
        adminToken,
        doctor.accessToken,
        fullName: 'STC003-03 Patient',
      );
      final patientToken = await loginRaw(setupDio, patient.phoneNumber, patient.password);
      // Đặt thẳng qua API — case này chỉ thật sự kiểm tra màn Hủy lịch, không lái lại toàn
      // bộ màn Đặt lịch chỉ để dựng dữ liệu tiền điều kiện.
      await bookAppointmentDirect(setupDio, patientToken, slotId, reason: 'STC003 checkup');

      await pumpApp(tester);
      await signIn(tester, patient.phoneNumber, patient.password);

      await waitForText(tester, 'Lịch khám của tôi');
      await tester.ensureVisible(find.text('Lịch khám của tôi'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Lịch khám của tôi'));
      await tester.pump(const Duration(milliseconds: 200));
      await waitForLoadingToFinish(tester);

      expect(find.byType(AppointmentCard), findsOneWidget);
      await tester.tap(find.byType(AppointmentCard));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Hủy lịch'));
      // Trước khi mở bottom sheet chọn lý do, màn hình tự gọi API kiểm tra số lần hủy hôm
      // nay (checkCancellationStatus()) — pumpAndSettle() đơn thuần đôi khi chạy trước khi
      // request thật đó kịp về, khiến sheet lý do chưa kịp mở lúc bước sau tìm nút bấm.
      await tester.pump(const Duration(milliseconds: 200));
      await waitForLoadingToFinish(tester);
      await settleBackgroundWork(tester);

      // Bottom sheet chọn lý do hủy (UC-14 BR-02) — lý do bắt buộc.
      await tester.tap(find.text('Bận công việc / Không sắp xếp được thời gian'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(ElevatedButton, 'XÁC NHẬN HỦY LỊCH'));
      await tester.pump(const Duration(milliseconds: 200));
      await waitForLoadingToFinish(tester);
      await settleBackgroundWork(tester);

      expect(find.text('Đã hủy lịch khám thành công.'), findsOneWidget);
    },
  );

  testWidgets(
    'STC004 — Chọn "Người thân" mà chưa chọn ai thì không đặt được, hiện đúng cảnh báo',
    (tester) async {
      final targetDate = pickBookableDate();
      final doctorName = 'STC004-03 Doctor ${uniqueSuffix()}';
      final doctor = await createReadyDoctor(setupDio, adminToken, fullName: doctorName);
      await ensureMorningSlotId(setupDio, doctor.accessToken, targetDate);
      // Patient KHÔNG có người thân nào — createRelative() không được gọi, đúng chủ đích.
      final patient = await createBookablePatient(
        setupDio,
        adminToken,
        doctor.accessToken,
        fullName: 'STC004-03 Patient',
      );

      await pumpApp(tester);
      await signIn(tester, patient.phoneNumber, patient.password);

      await waitForText(tester, 'Đặt lịch khám');
      await tester.ensureVisible(find.text('Đặt lịch khám'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Đặt lịch khám'));
      await tester.pump(const Duration(milliseconds: 200));
      await waitForLoadingToFinish(tester);

      await pickDoctorDateAndSlot(tester, doctorName, targetDate);

      await tapVisible(tester, find.text('Người thân'));

      // Không có người thân nào -> hiện khung cảnh báo, không phải dropdown chọn.
      expect(find.text('Chưa có người thân nào. Hãy thêm người thân để đặt lịch hộ.'),
          findsOneWidget);

      await tester.ensureVisible(find.widgetWithText(ElevatedButton, 'XÁC NHẬN ĐẶT LỊCH'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(ElevatedButton, 'XÁC NHẬN ĐẶT LỊCH'));
      await tester.pumpAndSettle();

      // Chặn phía client TRƯỚC khi gọi API — vẫn ở màn Đặt lịch, không có snackbar thành
      // công nào cả.
      expect(
        find.text('Vui lòng chọn người thân trước khi xác nhận đặt lịch.'),
        findsOneWidget,
      );
      expect(find.text('Đặt lịch khám'), findsWidgets);
    },
  );
}
