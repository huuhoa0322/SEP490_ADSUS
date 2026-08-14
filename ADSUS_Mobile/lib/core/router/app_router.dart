import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../features/auth/presentation/viewmodels/auth_view_model.dart';
import '../../features/auth/presentation/views/change_password_screen.dart';
import '../../features/auth/presentation/views/sign_in_screen.dart';
import '../../shared/main_shell.dart';

/// Chuyển thay đổi của [authViewModelProvider] thành sự kiện GoRouter hiểu được.
///
/// GoRouter chỉ gọi lại `redirect` khi Listenable này bắn `notifyListeners()` — không tự
/// theo dõi Riverpod. Không có lớp này, đăng nhập/đăng xuất/hết phiên xong màn hình vẫn
/// đứng yên ở route cũ, phải tự thao tác gì đó mới điều hướng.
class _AuthRefreshNotifier extends ChangeNotifier {
  _AuthRefreshNotifier(Ref ref) {
    ref.listen(authViewModelProvider, (_, _) => notifyListeners());
  }
}

/// Cổng vào duy nhất của app — tương đương AuthGate cũ, nhưng logic chuyển vào
/// GoRouter.redirect đúng theo 03_mobile.md §10, thay vì if/else trong build() của 1 widget.
///
/// Thứ tự ưu tiên bám đúng UCS:
///   1. Chưa đăng nhập            -> SCR-02 màn đăng nhập
///   2. Bị ép đổi mật khẩu        -> SCR-05, CHẶN mọi màn khác cho tới khi đổi xong (UC-25)
///   3. Bình thường               -> màn hình chính
///
/// Lưu ý: đây KHÔNG phải lớp bảo vệ dữ liệu. Lớp bảo vệ thật nằm ở [Authorize] phía
/// backend; mọi endpoint đều tự kiểm tra chữ ký token. Cổng này chỉ để trải nghiệm mượt.
///
/// PHẠM VI (14/08/2026): chỉ gắn go_router ở gốc app cho 3 điểm đến trên. Điều hướng bên
/// trong từng màn (chi tiết lịch hẹn, thêm nhật ký sức khoẻ, quên mật khẩu...) vẫn dùng
/// Navigator.push/pop như cũ — vẫn hợp lệ khi lồng bên trong 1 GoRoute, không cần viết lại.
final goRouterProvider = Provider<GoRouter>((ref) {
  final refreshNotifier = _AuthRefreshNotifier(ref);

  final router = GoRouter(
    initialLocation: '/sign-in',
    refreshListenable: refreshNotifier,
    redirect: (context, state) {
      final authState = ref.read(authViewModelProvider);
      final isSignedIn = authState.isSignedIn;
      final mustChangePassword = authState.session?.mustChangePassword ?? false;
      final isSignInRoute = state.matchedLocation == '/sign-in';
      final isChangePasswordRoute = state.matchedLocation == '/change-password';

      if (!isSignedIn) {
        return isSignInRoute ? null : '/sign-in';
      }

      // UC-25: tài khoản dùng mật khẩu tạm phải đổi trước khi vào bất kỳ màn nào khác.
      if (mustChangePassword) {
        return isChangePasswordRoute ? null : '/change-password';
      }

      // Đã đăng nhập xong, không còn bị ép đổi mật khẩu -> không được đứng ở 2 màn trên nữa.
      if (isSignInRoute || isChangePasswordRoute) {
        return '/';
      }

      return null;
    },
    routes: [
      GoRoute(path: '/sign-in', builder: (context, state) => const SignInScreen()),
      GoRoute(
        path: '/change-password',
        builder: (context, state) => const ChangePasswordScreen(),
      ),
      GoRoute(path: '/', builder: (context, state) => const MainShell()),
    ],
  );

  ref.onDispose(() {
    refreshNotifier.dispose();
    router.dispose();
  });

  return router;
});
