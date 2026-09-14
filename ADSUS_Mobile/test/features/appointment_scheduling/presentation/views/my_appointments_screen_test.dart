import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

import 'package:adsus_mobile/features/appointment_scheduling/data/dtos/appointment_dtos.dart';
import 'package:adsus_mobile/features/appointment_scheduling/domain/entities/appointment.dart';
import 'package:adsus_mobile/features/appointment_scheduling/domain/entities/appointment_summary.dart';
import 'package:adsus_mobile/features/appointment_scheduling/domain/entities/symptom.dart';
import 'package:adsus_mobile/features/appointment_scheduling/domain/repositories/appointment_repository.dart';
import 'package:adsus_mobile/features/appointment_scheduling/domain/repositories/symptom_repository.dart';
import 'package:adsus_mobile/features/appointment_scheduling/presentation/views/my_appointments_screen.dart';
import 'package:adsus_mobile/features/appointment_scheduling/presentation/views/widgets/edit_clinical_info_sheet.dart';
import 'package:adsus_mobile/features/appointment_scheduling/domain/services/calendar_sync_service.dart';
import 'package:adsus_mobile/features/auth/presentation/viewmodels/auth_view_model.dart';
import 'package:adsus_mobile/shared/providers/app_providers.dart';

class _MockAppointmentRepo extends Mock implements AppointmentRepository {}
class _MockSymptomRepo extends Mock implements SymptomRepository {}
class _MockCalendarSyncService extends Mock implements CalendarSyncService {}

class _FakeAuthViewModel extends StateNotifier<AuthState> implements AuthViewModel {
  _FakeAuthViewModel() : super(const AuthState());

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

void main() {
  late _MockAppointmentRepo mockApptRepo;
  late _MockSymptomRepo mockSymptomRepo;

  final sampleSelf = AppointmentSummary(
    id: 'appt-self-1',
    slotId: 'slot-1',
    patientProfileId: 'profile-self',
    patientFullName: 'Nguyễn Văn Tôi',
    patientPhone: '0912345678',
    status: AppointmentStatus.booked,
    reason: 'Đau đầu căng thẳng',
    createdAt: DateTime(2026, 9, 10, 8, 0),
    slotDate: DateTime(2026, 9, 20),
    startTime: '08:30',
    endTime: '09:00',
    doctorId: 'doc-1',
    doctorName: 'Lê Minh',
    isBookedForOthers: false,
  );

  final sampleRelative = AppointmentSummary(
    id: 'appt-rel-1',
    slotId: 'slot-2',
    patientProfileId: 'profile-rel',
    patientFullName: 'Mẹ Trần Thị Hoa',
    patientPhone: '0987654321',
    status: AppointmentStatus.booked,
    reason: 'Đau khớp gối kéo dài',
    createdAt: DateTime(2026, 9, 10, 9, 0),
    slotDate: DateTime(2026, 9, 21),
    startTime: '10:00',
    endTime: '10:30',
    doctorId: 'doc-2',
    doctorName: 'Nguyễn Mai',
    isBookedForOthers: true,
    relationshipLabel: 'Mẹ',
    bookedByUserName: 'Con Trai',
  );

  setUp(() {
    mockApptRepo = _MockAppointmentRepo();
    mockSymptomRepo = _MockSymptomRepo();

    when(() => mockApptRepo.listMyAppointments())
        .thenAnswer((_) async => [sampleSelf, sampleRelative]);
    when(() => mockApptRepo.getCancellationStatusToday()).thenAnswer((_) async =>
        CancellationStatusTodayDto(
          cancellationsToday: 0,
          maxCancellations: 3,
          canBookOnline: true,
          isNextCancellationFinal: false,
        ));
    when(() => mockSymptomRepo.getCategories()).thenAnswer((_) async => []);
  });

  Widget buildWidget({List<Override> overrides = const []}) {
    final mockCalendar = _MockCalendarSyncService();
    when(() => mockCalendar.hasSynced(any())).thenAnswer((_) async => false);

    return ProviderScope(
      overrides: [
        appointmentRepositoryProvider.overrideWithValue(mockApptRepo),
        symptomRepositoryProvider.overrideWithValue(mockSymptomRepo),
        calendarSyncServiceProvider.overrideWith((ref) async => mockCalendar),
        authViewModelProvider.overrideWith((ref) => _FakeAuthViewModel()),
        ...overrides,
      ],
      child: const MaterialApp(
        home: MyAppointmentsScreen(),
      ),
    );
  }

  group('MyAppointmentsScreen 2-Tab & Widget Tests', () {
    testWidgets('hiển thị 2 tab với số lượng tương ứng: Lịch của tôi (1) và Lịch người thân (1)',
        (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(buildWidget());
      await tester.pumpAndSettle();

      expect(find.text('Lịch của tôi (1)'), findsOneWidget);
      expect(find.text('Lịch người thân (1)'), findsOneWidget);

      // Default tab là 'SELF' -> hiển thị lịch của tôi, không hiển thị thẻ đặt hộ
      expect(find.text('BS. Lê Minh'), findsOneWidget);
      expect(find.text('08:30 - 09:00'), findsOneWidget);
      expect(find.textContaining('Đặt hộ:'), findsNothing);

      // Chuyển sang tab "Lịch người thân (1)"
      await tester.tap(find.text('Lịch người thân (1)'));
      await tester.pumpAndSettle();

      // Tab người thân hiển thị thẻ người thân với badge Đặt hộ và tên người khám
      expect(find.text('BS. Nguyễn Mai'), findsOneWidget);
      expect(find.text('Mẹ Trần Thị Hoa'), findsOneWidget);
      expect(find.text('Đặt hộ: Mẹ'), findsOneWidget);
      expect(find.text('10:00 - 10:30'), findsOneWidget);
    });

    testWidgets('hiển thị Empty State riêng biệt cho từng tab khi không có dữ liệu',
        (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      when(() => mockApptRepo.listMyAppointments()).thenAnswer((_) async => []);

      await tester.pumpWidget(buildWidget());
      await tester.pumpAndSettle();

      // Tab SELF empty state
      expect(find.text('Bạn chưa có lịch khám nào cho bản thân.'), findsOneWidget);
      expect(find.text('ĐẶT LỊCH NGAY'), findsOneWidget);

      // Đổi sang tab RELATIVE
      await tester.tap(find.text('Lịch người thân (0)'));
      await tester.pumpAndSettle();

      // Tab RELATIVE empty state
      expect(find.text('Bạn chưa có lịch khám nào đặt cho người thân.'), findsOneWidget);
      expect(find.text('ĐẶT LỊCH CHO NGƯỜI THÂN'), findsOneWidget);
    });

    testWidgets('cảnh báo hủy lần 3: hiển thị AlertDialog màu cam khi isNextCancellationFinal = true',
        (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      // Giả lập user đã có 2 lần hủy hôm nay
      when(() => mockApptRepo.getCancellationStatusToday()).thenAnswer((_) async =>
          CancellationStatusTodayDto(
            cancellationsToday: 2,
            maxCancellations: 3,
            canBookOnline: true,
            isNextCancellationFinal: true,
          ));

      await tester.pumpWidget(buildWidget());
      await tester.pumpAndSettle();

      // Bấm vào appointment card để mở sheet chi tiết
      await tester.tap(find.text('08:30 - 09:00'));
      await tester.pumpAndSettle();

      // Trong sheet chi tiết, bấm nút "Hủy lịch"
      final cancelBtn = find.widgetWithText(OutlinedButton, 'Hủy lịch');
      expect(cancelBtn, findsOneWidget);

      await tester.tap(cancelBtn);
      await tester.pumpAndSettle();

      // Kiểm tra dialog cảnh báo lần 3 xuất hiện
      expect(find.text('Cảnh báo lượt hủy cuối'), findsOneWidget);
      expect(find.textContaining('Bạn đã hủy 2 lần trong ngày hôm nay.'), findsOneWidget);
      expect(find.textContaining('Nếu bạn hủy lần này (lần thứ 3)'), findsOneWidget);
      expect(find.text('Quay lại'), findsOneWidget);
      expect(find.text('Tiếp tục hủy'), findsOneWidget);

      // Bấm "Quay lại" -> dialog đóng lại, không gọi cancel
      await tester.tap(find.text('Quay lại'));
      await tester.pumpAndSettle();

      expect(find.text('Cảnh báo lượt hủy cuối'), findsNothing);
      verifyNever(() => mockApptRepo.cancelMyAppointment(
            id: any(named: 'id'),
            cancellationReason: any(named: 'cancellationReason'),
          ));
    });

    testWidgets('mở sheet Sửa thông tin khám từ chi tiết cuộc hẹn, pre-fill lý do và gửi cập nhật',
        (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      final category = SymptomCategory(
        id: 'cat-1',
        name: 'Thần kinh',
        symptoms: const [
          Symptom(id: 'sym-1', name: 'Đau nửa đầu'),
        ],
      );
      when(() => mockSymptomRepo.getCategories()).thenAnswer((_) async => [category]);

      final updatedAppointment = Appointment(
        id: 'appt-self-1',
        slotId: 'slot-1',
        patientProfileId: 'profile-self',
        patientFullName: 'Nguyễn Văn Tôi',
        status: AppointmentStatus.booked,
        reason: 'Đau đầu sau gáy kéo dài',
        createdAt: DateTime(2026, 9, 10, 8, 0),
        updatedAt: DateTime.now(),
      );

      when(() => mockApptRepo.updateClinicalInfo(
            'appt-self-1',
            reason: any(named: 'reason'),
            symptoms: any(named: 'symptoms'),
          )).thenAnswer((_) async => updatedAppointment);

      await tester.pumpWidget(buildWidget());
      await tester.pumpAndSettle();

      // Bấm vào appointment card để mở AppointmentDetailSheet
      await tester.tap(find.text('08:30 - 09:00'));
      await tester.pumpAndSettle();

      // Kiểm tra sheet chi tiết mở ra và có nút "Sửa thông tin khám"
      expect(find.text('Chi tiết lịch khám'), findsOneWidget);
      final editBtn = find.widgetWithText(OutlinedButton, 'Sửa thông tin khám');
      expect(editBtn, findsOneWidget);

      // Bấm nút "Sửa thông tin khám"
      await tester.tap(editBtn);
      await tester.pumpAndSettle();

      // Kiểm tra EditClinicalInfoSheet hiển thị
      expect(find.byType(EditClinicalInfoSheet), findsOneWidget);
      expect(find.text('Sửa thông tin khám'), findsOneWidget);

      // Pre-fill lý do ban đầu
      expect(find.text('Đau đầu căng thẳng'), findsOneWidget);

      // Nhập lý do mới vào TextField
      final reasonField = find.byType(TextField).first;
      await tester.enterText(reasonField, 'Đau đầu sau gáy kéo dài');
      await tester.pumpAndSettle();

      // Bấm nút "Lưu thay đổi"
      final saveBtn = find.text('Lưu thay đổi');
      await tester.ensureVisible(saveBtn);
      await tester.tap(saveBtn);
      await tester.pumpAndSettle();

      // Xác nhận API updateClinicalInfo được gọi với reason mới
      verify(() => mockApptRepo.updateClinicalInfo(
            'appt-self-1',
            reason: 'Đau đầu sau gáy kéo dài',
            symptoms: any(named: 'symptoms'),
          )).called(1);

      // Kiểm tra snackbar thành công
      expect(find.text('Cập nhật thông tin khám thành công!'), findsOneWidget);
    });
  });
}
