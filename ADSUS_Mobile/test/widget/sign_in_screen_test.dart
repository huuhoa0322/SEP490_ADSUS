import 'package:adsus_mobile/features/auth/presentation/views/sign_in_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

/// Kiểm thử màn đăng nhập SCR-02 (UC-01).
///
/// Chỉ dựng widget, không gọi API — phần nghiệp vụ đã có 85 unit test bên backend.
/// Ở đây chỉ khẳng định giao diện hiện đúng và luật nhập liệu phía client chạy đúng.
void main() {
  Widget dungManDangNhap() => const ProviderScope(
        child: MaterialApp(home: SignInScreen()),
      );

  testWidgets('Hien du o nhap va nut dang nhap', (tester) async {
    await tester.pumpWidget(dungManDangNhap());

    expect(find.text('Đăng nhập'), findsOneWidget);
    expect(find.text('SỐ ĐIỆN THOẠI'), findsOneWidget);
    expect(find.text('MẬT KHẨU'), findsOneWidget);
    expect(find.text('ĐĂNG NHẬP'), findsOneWidget);
  });

  testWidgets('Bo trong o nhap thi bao loi, khong goi API', (tester) async {
    await tester.pumpWidget(dungManDangNhap());

    await tester.tap(find.text('ĐĂNG NHẬP'));
    await tester.pump();

    expect(
      find.text('Vui lòng nhập số điện thoại và mật khẩu.'),
      findsOneWidget,
    );
  });

  testWidgets('Mat khau bi che, bam vao mat mo hien duoc', (tester) async {
    await tester.pumpWidget(dungManDangNhap());

    // Ô mật khẩu là TextField thứ hai trên màn hình.
    final oMatKhau = tester.widget<TextField>(find.byType(TextField).at(1));
    expect(oMatKhau.obscureText, isTrue);

    await tester.tap(find.byIcon(Icons.visibility_outlined));
    await tester.pump();

    final sauKhiBam = tester.widget<TextField>(find.byType(TextField).at(1));
    expect(sauKhiBam.obscureText, isFalse);
  });

  testWidgets('Nut dang nhap van tay KHONG hien khi chua ghep doi thiet bi',
      (tester) async {
    // UC-02 BR-01: chưa từng đăng nhập bằng mật khẩu trên máy này thì không được
    // phép dùng sinh trắc học.
    await tester.pumpWidget(dungManDangNhap());
    await tester.pump();

    expect(find.text('ĐĂNG NHẬP BẰNG VÂN TAY'), findsNothing);
  });
}
