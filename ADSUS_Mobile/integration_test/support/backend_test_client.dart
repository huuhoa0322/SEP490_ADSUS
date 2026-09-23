import 'package:dio/dio.dart';

/// Client Dio "trần" dùng riêng để DỰNG dữ liệu test qua API thật trước khi lái UI (tạo
/// tài khoản qua Admin, khoá tài khoản...) — tách biệt hoàn toàn khỏi Dio thật của app.
/// Dio của app có interceptor tự đọc token từ FlutterSecureStorage, không phù hợp để tự
/// tay gắn Bearer token của một tài khoản Admin không liên quan tới phiên đang test.
///
/// Cùng backend, cùng DB test Supabase riêng mà ADSUS_BE.SystemTests (BF-01→11 + NFR) đã
/// dùng — trỏ qua `--dart-define=API_BASE_URL=...` giống hệt quy ước đã có trong
/// api_constants.dart.
Dio createSetupDio() {
  const baseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://0.0.0.0:5036',
  );
  return Dio(BaseOptions(
    baseUrl: baseUrl,
    connectTimeout: const Duration(seconds: 30),
    receiveTimeout: const Duration(seconds: 30),
  ));
}

/// Tài khoản Admin seed sẵn trong DB test — cùng giá trị `SeedAdminPhone`/`SeedAdminPassword`
/// dùng xuyên suốt mọi file BF trong ADSUS_BE.SystemTests, không phải giá trị mới bịa ra
/// riêng cho mobile.
const String seedAdminPhone = '0900000001';
const String seedAdminPassword = 'Aa123456@';

Future<String> loginAsAdmin(Dio dio) async {
  final res = await dio.post<Map<String, dynamic>>(
    '/api/v1/auth/login',
    data: {'phoneNumber': seedAdminPhone, 'password': seedAdminPassword},
  );
  final data = res.data!['data'] as Map<String, dynamic>;
  return data['accessToken'] as String;
}

/// Số điện thoại giả, không trùng qua các lần chạy — cùng tinh thần `UniquePhone()` phía
/// ADSUS_BE.SystemTests. Dữ liệu tích luỹ dần trong DB test, không tự dọn dẹp (quyết định
/// đã chốt khi viết bộ System Test backend, giữ nguyên cho mobile).
String uniquePhone() {
  final millis = DateTime.now().millisecondsSinceEpoch.toString();
  return '09${millis.substring(millis.length - 8)}';
}

class CreatedAccount {
  const CreatedAccount({
    required this.userId,
    required this.phoneNumber,
    required this.temporaryPassword,
  });

  final String userId;
  final String phoneNumber;
  final String temporaryPassword;
}

Future<CreatedAccount> createAccount(
  Dio dio,
  String adminToken, {
  required String fullName,
  required String role,
}) async {
  final phone = uniquePhone();
  final res = await dio.post<Map<String, dynamic>>(
    '/api/v1/admin/users',
    data: {'phoneNumber': phone, 'fullName': fullName, 'role': role},
    options: Options(headers: {'Authorization': 'Bearer $adminToken'}),
  );
  final data = res.data!['data'] as Map<String, dynamic>;
  final account = data['account'] as Map<String, dynamic>;
  return CreatedAccount(
    userId: account['userId'] as String,
    phoneNumber: phone,
    temporaryPassword: data['temporaryPassword'] as String,
  );
}

Future<void> deactivateAccount(Dio dio, String adminToken, String userId) async {
  await dio.put<void>(
    '/api/v1/admin/users/$userId/deactivate',
    options: Options(headers: {'Authorization': 'Bearer $adminToken'}),
  );
}

/// Tạo 1 tài khoản Patient rồi tự đổi luôn mật khẩu tạm QUA API (không qua UI) để
/// `mustChangePassword` = false ngay — dùng cho các test chỉ cần MỘT tài khoản Patient
/// đã sẵn sàng đăng nhập bình thường. Luồng ép đổi mật khẩu lần đầu qua UI thật đã có
/// STC-M005 kiểm tra riêng, không lặp lại ở đây.
Future<({String phoneNumber, String password})> createReadyPatient(
  Dio dio,
  String adminToken, {
  required String fullName,
  String finalPassword = 'Patient@2026',
}) async {
  final created = await createAccount(dio, adminToken, fullName: fullName, role: 'PATIENT');
  final password = await _loginThenChangePassword(dio, created.phoneNumber, created.temporaryPassword, finalPassword);
  return (phoneNumber: created.phoneNumber, password: password);
}

/// Đăng nhập bằng mật khẩu tạm rồi đổi ngay qua API, trả về mật khẩu cuối cùng — logic dùng
/// chung cho cả Patient (createReadyPatient) và Doctor (createReadyDoctor) nên tách riêng.
Future<String> _loginThenChangePassword(
  Dio dio,
  String phoneNumber,
  String temporaryPassword,
  String finalPassword,
) async {
  final tempToken = await loginRaw(dio, phoneNumber, temporaryPassword);
  await dio.post<void>(
    '/api/v1/auth/change-password',
    data: {
      'currentPassword': temporaryPassword,
      'newPassword': finalPassword,
      'confirmNewPassword': finalPassword,
    },
    options: Options(headers: {'Authorization': 'Bearer $tempToken'}),
  );
  return finalPassword;
}

/// Đăng nhập thật, trả về access token — dùng khi test cần TỰ gọi API bằng vai trò đó
/// (không qua UI), ví dụ Doctor gọi ensure-default/patient-profiles, hay Patient tự đặt
/// lịch thẳng qua API để dựng dữ liệu sẵn cho 1 case chỉ tập trung kiểm tra màn hủy lịch.
Future<String> loginRaw(Dio dio, String phoneNumber, String password) async {
  final res = await dio.post<Map<String, dynamic>>(
    '/api/v1/auth/login',
    data: {'phoneNumber': phoneNumber, 'password': password},
  );
  final data = res.data!['data'] as Map<String, dynamic>;
  return data['accessToken'] as String;
}

class ReadyDoctor {
  const ReadyDoctor({
    required this.phoneNumber,
    required this.password,
    required this.accessToken,
  });

  final String phoneNumber;
  final String password;

  /// Token MỚI trả về ngay lúc đổi mật khẩu — không cần đăng nhập lại lần 2 (xem cùng bug
  /// đã tìm thấy và sửa ở mobile app thật: AuthRepositoryImpl.changePassword() trước đây bỏ
  /// qua token mới này, ở đây tự đọc đúng ngay từ đầu).
  final String accessToken;
}

Future<ReadyDoctor> createReadyDoctor(
  Dio dio,
  String adminToken, {
  required String fullName,
  String finalPassword = 'Doctor@2026',
}) async {
  final created = await createAccount(dio, adminToken, fullName: fullName, role: 'DOCTOR');
  final tempToken = await loginRaw(dio, created.phoneNumber, created.temporaryPassword);
  final changeRes = await dio.post<Map<String, dynamic>>(
    '/api/v1/auth/change-password',
    data: {
      'currentPassword': created.temporaryPassword,
      'newPassword': finalPassword,
      'confirmNewPassword': finalPassword,
    },
    options: Options(headers: {'Authorization': 'Bearer $tempToken'}),
  );
  final freshToken =
      (changeRes.data!['data'] as Map<String, dynamic>)['accessToken'] as String;
  return ReadyDoctor(
    phoneNumber: created.phoneNumber,
    password: finalPassword,
    accessToken: freshToken,
  );
}

String _formatIsoDate(DateTime d) =>
    '${d.year.toString().padLeft(4, '0')}-'
    '${d.month.toString().padLeft(2, '0')}-'
    '${d.day.toString().padLeft(2, '0')}';

/// Sinh đủ 16 ca/ngày (T2-CN) cho tuần chứa [visitDate] (idempotent — gọi lại nhiều lần
/// không tạo trùng), rồi lấy ca buổi sáng sớm nhất (StartTime < 12:00) còn Open trong ngày
/// đó. Cùng logic `CreateSlotAsync()` phía `ADSUS_BE.SystemTests/BF03_AppointmentBooking` —
/// endpoint tạo 1 slot thủ công đã bị gỡ khỏi backend, slot chỉ sinh được qua ensure-default.
Future<String> ensureMorningSlotId(
  Dio dio,
  String doctorToken,
  DateTime visitDate,
) async {
  final dayOffsetFromMonday = (visitDate.weekday - 1) % 7;
  final weekStart = visitDate.subtract(Duration(days: dayOffsetFromMonday));

  await dio.post<void>(
    '/api/v1/schedule-slots/ensure-default',
    queryParameters: {'weekStart': _formatIsoDate(weekStart)},
    options: Options(headers: {'Authorization': 'Bearer $doctorToken'}),
  );

  final dateStr = _formatIsoDate(visitDate);
  final res = await dio.get<Map<String, dynamic>>(
    '/api/v1/schedule-slots',
    queryParameters: {'fromDate': dateStr, 'toDate': dateStr, 'status': 'Open'},
    options: Options(headers: {'Authorization': 'Bearer $doctorToken'}),
  );
  final data = res.data!['data'] as Map<String, dynamic>;
  final items = (data['items'] as List).cast<Map<String, dynamic>>();
  final morning = items
      .where((s) => (s['startTime'] as String).compareTo('12:00') < 0)
      .toList()
    ..sort((a, b) => (a['startTime'] as String).compareTo(b['startTime'] as String));
  return morning.first['slotId'] as String;
}

/// UC-13: Patient không đặt lịch được nếu chưa có PatientProfile — hồ sơ này chỉ Doctor
/// (hoặc Nurse/Staff) lập được qua API, không có màn hình nào trên Mobile cho Patient tự
/// tạo hồ sơ của chính mình (đúng thiết kế thật, đọc từ AppointmentsController phía backend).
Future<void> createPatientProfile(
  Dio dio,
  String doctorToken,
  String patientUserId, {
  String gender = 'FEMALE',
}) async {
  await dio.post<void>(
    '/api/v1/patient-profiles',
    data: {
      'patientUserId': patientUserId,
      'gender': gender,
      'diseases': null,
      'allergies': null,
    },
    options: Options(headers: {'Authorization': 'Bearer $doctorToken'}),
  );
}

class BookablePatient {
  const BookablePatient({
    required this.phoneNumber,
    required this.password,
    required this.userId,
  });

  final String phoneNumber;
  final String password;
  final String userId;
}

/// Tạo 1 Patient đã sẵn sàng đặt lịch thật: tài khoản + mật khẩu cuối cùng (qua API, không
/// qua UI — luồng ép đổi mật khẩu lần đầu đã có test riêng ở BF-01) + đã có PatientProfile
/// do [doctorToken] lập hộ (điều kiện bắt buộc, xem createPatientProfile()).
Future<BookablePatient> createBookablePatient(
  Dio dio,
  String adminToken,
  String doctorToken, {
  required String fullName,
  String finalPassword = 'Patient@2026',
  String gender = 'FEMALE',
}) async {
  final created = await createAccount(dio, adminToken, fullName: fullName, role: 'PATIENT');
  await createPatientProfile(dio, doctorToken, created.userId, gender: gender);
  final password =
      await _loginThenChangePassword(dio, created.phoneNumber, created.temporaryPassword, finalPassword);
  return BookablePatient(
    phoneNumber: created.phoneNumber,
    password: password,
    userId: created.userId,
  );
}

/// Đặt lịch THẲNG qua API (không qua UI) — dùng để dựng sẵn 1 cuộc hẹn cho các case chỉ
/// thật sự kiểm tra màn hình khác (ví dụ màn hủy lịch), tránh phải lái lại toàn bộ màn đặt
/// lịch (DropdownSearch bác sĩ, chọn tuần/ngày...) chỉ để dựng dữ liệu tiền điều kiện.
Future<void> bookAppointmentDirect(
  Dio dio,
  String patientToken,
  String scheduleSlotId, {
  String? relationshipId,
  String? reason,
}) async {
  final body = <String, dynamic>{'scheduleSlotId': scheduleSlotId};
  if (relationshipId != null) body['relationshipId'] = relationshipId;
  if (reason != null) body['reason'] = reason;
  await dio.post<void>(
    '/api/v1/appointments',
    data: body,
    options: Options(headers: {'Authorization': 'Bearer $patientToken'}),
  );
}

/// Thêm 1 người thân qua API (không qua UI — màn Thêm người thân không thuộc phạm vi BF-03).
/// Trả về relationshipId, cần để chọn đúng mục trong dropdown khi đặt lịch hộ qua UI.
Future<String> createRelative(
  Dio dio,
  String patientToken, {
  required String fullName,
  required String relationshipName,
}) async {
  final res = await dio.post<Map<String, dynamic>>(
    '/api/v1/relatives',
    data: {'fullName': fullName, 'relationshipName': relationshipName},
    options: Options(headers: {'Authorization': 'Bearer $patientToken'}),
  );
  final body = res.data!;
  if (body['relationshipId'] != null) return body['relationshipId'] as String;
  final data = body['data'] as Map<String, dynamic>;
  return data['relationshipId'] as String;
}
