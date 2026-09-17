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
import 'package:adsus_mobile/features/appointment_scheduling/domain/services/calendar_sync_service.dart';
import 'package:adsus_mobile/features/appointment_scheduling/presentation/views/my_appointments_screen.dart';
import 'package:adsus_mobile/features/appointment_scheduling/presentation/views/widgets/appointment_detail_sheet.dart';
import 'package:adsus_mobile/features/appointment_scheduling/presentation/views/widgets/cancel_reason_sheet.dart';
import 'package:adsus_mobile/features/appointment_scheduling/presentation/views/widgets/edit_clinical_info_sheet.dart';
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
  late _MockCalendarSyncService mockCalendar;

  final sampleSelfAppt = AppointmentSummary(
    id: 'appt-self-101',
    slotId: 'slot-101',
    patientProfileId: 'profile-self',
    patientFullName: 'Nguyễn Văn An',
    patientPhone: '0901112233',
    status: AppointmentStatus.booked,
    reason: 'Đau đầu mất ngủ',
    createdAt: DateTime(2026, 9, 12, 8, 0),
    slotDate: DateTime(2026, 9, 22),
    startTime: '08:30',
    endTime: '09:00',
    doctorId: 'doc-101',
    doctorName: 'Lê Minh',
    isBookedForOthers: false,
  );

  final sampleRelativeAppt = AppointmentSummary(
    id: 'appt-rel-202',
    slotId: 'slot-202',
    patientProfileId: 'profile-mother',
    patientFullName: 'Bà Nguyễn Thị Mẹ',
    patientPhone: '0909998877',
    status: AppointmentStatus.booked,
    reason: 'Đau khớp gối',
    createdAt: DateTime(2026, 9, 12, 9, 0),
    slotDate: DateTime(2026, 9, 23),
    startTime: '10:00',
    endTime: '10:30',
    doctorId: 'doc-202',
    doctorName: 'Nguyễn Mai',
    isBookedForOthers: true,
    relationshipLabel: 'Mẹ',
    bookedByUserName: 'Nguyễn Văn An',
  );

  setUp(() {
    mockApptRepo = _MockAppointmentRepo();
    mockSymptomRepo = _MockSymptomRepo();
    mockCalendar = _MockCalendarSyncService();

    when(() => mockCalendar.hasSynced(any())).thenAnswer((_) async => false);
    when(() => mockApptRepo.listMyAppointments())
        .thenAnswer((_) async => [sampleSelfAppt, sampleRelativeAppt]);
    when(() => mockApptRepo.getCancellationStatusToday()).thenAnswer((_) async =>
        CancellationStatusTodayDto(
          cancellationsToday: 0,
          maxCancellations: 3,
          canBookOnline: true,
          isNextCancellationFinal: false,
        ));
    when(() => mockSymptomRepo.getCategories()).thenAnswer((_) async => const [
          SymptomCategory(
            id: 'cat-xuong-khop',
            name: 'Cơ xương khớp',
            symptoms: [
              Symptom(id: 'sym-dau-khop', name: 'Đau sưng khớp'),
            ],
          ),
        ]);
  });

  Widget createE2eApp() {
    return ProviderScope(
      overrides: [
        appointmentRepositoryProvider.overrideWithValue(mockApptRepo),
        symptomRepositoryProvider.overrideWithValue(mockSymptomRepo),
        calendarSyncServiceProvider.overrideWith((ref) async => mockCalendar),
        authViewModelProvider.overrideWith((ref) => _FakeAuthViewModel()),
      ],
      child: const MaterialApp(
        home: MyAppointmentsScreen(),
      ),
    );
  }

  group('Kịch bản E2E Flow Test Hoàn Chỉnh - MyAppointmentsScreen', () {
    testWidgets(
        'E2E Full Flow: Mở danh sách -> Chuyển tab Người thân -> Sửa thông tin khám -> Cảnh báo hủy lần 3',
        (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      // =======================================================================
      // BƯỚC 1: Khởi động màn hình danh sách lịch khám
      // =======================================================================
      await tester.pumpWidget(createE2eApp());
      await tester.pumpAndSettle();

      // Kiểm tra tiêu đề màn hình và SegmentedButton hiển thị đủ 2 tab với số đếm
      expect(find.text('Lịch khám của tôi'), findsOneWidget);
      expect(find.text('Lịch của tôi (1)'), findsOneWidget);
      expect(find.text('Lịch người thân (1)'), findsOneWidget);

      // Tab mặc định là "Lịch của tôi" -> Thấy cuộc hẹn của bản thân
      expect(find.text('BS. Lê Minh'), findsOneWidget);
      expect(find.text('08:30 - 09:00'), findsOneWidget);
      expect(find.textContaining('Đặt hộ:'), findsNothing);

      // =======================================================================
      // BƯỚC 2: Bấm chuyển tab "Lịch người thân" -> Kiểm tra thẻ đặt hộ và nhãn quan hệ
      // =======================================================================
      final relativeTab = find.text('Lịch người thân (1)');
      await tester.tap(relativeTab);
      await tester.pumpAndSettle();

      // Kiểm tra thông tin người thân trên AppointmentCard
      expect(find.text('BS. Nguyễn Mai'), findsOneWidget);
      expect(find.text('10:00 - 10:30'), findsOneWidget);
      expect(find.text('Đặt hộ: Mẹ'), findsOneWidget);
      expect(find.text('Bà Nguyễn Thị Mẹ'), findsOneWidget);

      // =======================================================================
      // BƯỚC 3: Bấm vào chi tiết cuộc hẹn -> Bấm "Sửa thông tin khám" -> Sửa dữ liệu và submit
      // =======================================================================
      // Bấm vào card lịch của người thân
      await tester.tap(find.text('10:00 - 10:30'));
      await tester.pumpAndSettle();

      // Modal bottom sheet "Chi tiết lịch khám" mở ra
      expect(find.text('Chi tiết lịch khám'), findsOneWidget);
      expect(
        find.descendant(
          of: find.byType(AppointmentDetailSheet),
          matching: find.text('Bà Nguyễn Thị Mẹ'),
        ),
        findsOneWidget,
      );
      expect(
        find.descendant(
          of: find.byType(AppointmentDetailSheet),
          matching: find.text('Mẹ'),
        ),
        findsOneWidget,
      );

      // Nút "Sửa thông tin khám" xuất hiện
      final editBtn = find.widgetWithText(OutlinedButton, 'Sửa thông tin khám');
      expect(editBtn, findsOneWidget);

      // Chuẩn bị mock cho updateClinicalInfo
      final updatedRelativeAppt = Appointment(
        id: 'appt-rel-202',
        slotId: 'slot-202',
        patientProfileId: 'profile-mother',
        patientFullName: 'Bà Nguyễn Thị Mẹ',
        status: AppointmentStatus.booked,
        reason: 'Đau khớp gối và sưng phù mắt cá chân',
        isBookedForOthers: true,
        relationshipLabel: 'Mẹ',
        createdAt: DateTime(2026, 9, 12, 9, 0),
        updatedAt: DateTime.now(),
      );
      when(() => mockApptRepo.updateClinicalInfo(
            'appt-rel-202',
            reason: any(named: 'reason'),
            symptoms: any(named: 'symptoms'),
          )).thenAnswer((_) async => updatedRelativeAppt);

      // Bấm "Sửa thông tin khám"
      await tester.tap(editBtn);
      await tester.pumpAndSettle();

      // EditClinicalInfoSheet hiển thị
      expect(find.byType(EditClinicalInfoSheet), findsOneWidget);
      expect(find.text('Sửa thông tin khám'), findsOneWidget);

      // Kiểm tra pre-fill lý do cũ: "Đau khớp gối"
      expect(find.text('Đau khớp gối'), findsOneWidget);

      // Nhập lý do khám mới
      final reasonField = find.byType(TextField).first;
      await tester.enterText(reasonField, 'Đau khớp gối và sưng phù mắt cá chân');
      await tester.pumpAndSettle();

      // Bấm "Lưu thay đổi"
      final saveBtn = find.text('Lưu thay đổi');
      await tester.ensureVisible(saveBtn);
      await tester.tap(saveBtn);
      await tester.pumpAndSettle();

      // Xác minh API updateClinicalInfo được gọi đúng params
      verify(() => mockApptRepo.updateClinicalInfo(
            'appt-rel-202',
            reason: 'Đau khớp gối và sưng phù mắt cá chân',
            symptoms: any(named: 'symptoms'),
          )).called(1);

      // Xác nhận snackbar thành công
      expect(find.text('Cập nhật thông tin khám thành công!'), findsOneWidget);

      // =======================================================================
      // BƯỚC 4: Bấm nút hủy lịch khi đã có 2 lần hủy trước đó -> Kiểm tra hộp thoại cảnh báo lần 3 màu cam
      // =======================================================================
      // Giả lập trạng thái user đã có 2 lần hủy hôm nay (isNextCancellationFinal = true)
      when(() => mockApptRepo.getCancellationStatusToday()).thenAnswer((_) async =>
          CancellationStatusTodayDto(
            cancellationsToday: 2,
            maxCancellations: 3,
            canBookOnline: true,
            isNextCancellationFinal: true,
          ));

      // Mở lại chi tiết cuộc hẹn
      await tester.tap(find.text('10:00 - 10:30'));
      await tester.pumpAndSettle();

      // Tìm và bấm nút "Hủy lịch"
      final cancelBtn = find.widgetWithText(OutlinedButton, 'Hủy lịch');
      expect(cancelBtn, findsOneWidget);

      await tester.tap(cancelBtn);
      await tester.pumpAndSettle();

      // Xác nhận hộp thoại cảnh báo màu cam xuất hiện
      expect(find.text('Cảnh báo lượt hủy cuối'), findsOneWidget);
      expect(find.byIcon(Icons.warning_amber_rounded), findsOneWidget);
      expect(
        find.textContaining('Bạn đã hủy 2 lần trong ngày hôm nay.'),
        findsOneWidget,
      );
      expect(
        find.textContaining(
            'Nếu bạn hủy lần này (lần thứ 3), quyền tự đặt lịch trực tuyến của bạn sẽ bị tạm khóa đến hết ngày hôm nay.'),
        findsOneWidget,
      );

      // Xác nhận có đầy đủ nút hành động: "Quay lại" và "Tiếp tục hủy"
      expect(find.widgetWithText(OutlinedButton, 'Quay lại'), findsOneWidget);
      expect(find.widgetWithText(ElevatedButton, 'Tiếp tục hủy'), findsOneWidget);

      // Bấm "Tiếp tục hủy" để tiến hành nhập lý do hủy
      await tester.tap(find.widgetWithText(ElevatedButton, 'Tiếp tục hủy'));
      await tester.pumpAndSettle();

      // Cảnh báo đóng lại và mở sheet chọn lý do hủy
      expect(find.text('Cảnh báo lượt hủy cuối'), findsNothing);
      expect(find.byType(CancelReasonSheet), findsOneWidget);
      expect(find.text('Hủy lịch khám'), findsOneWidget);
    });
  });
}
