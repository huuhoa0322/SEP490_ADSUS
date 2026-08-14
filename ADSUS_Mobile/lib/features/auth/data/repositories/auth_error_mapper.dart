import 'package:dio/dio.dart';

import '../../../../core/network/api_exception.dart';

/// Dịch lỗi đăng nhập sang tiếng Việt — luật riêng của Module 1 Auth (UC-01 GB-06), không
/// dùng chung với module khác nên KHÔNG đặt trong core/network/api_exception.dart (chỗ đó
/// chỉ giữ [ApiErrorMapper.general], dùng chung mọi module). Tương đương
/// getSignInErrorMessage bên Web, nằm ở features/auth/lib/auth-messages.ts thay vì
/// lib/api-client.ts dùng chung.
class AuthErrorMapper {
  const AuthErrorMapper._();

  /// Câu DUY NHẤT hiển thị cho mọi trường hợp đăng nhập thất bại.
  ///
  /// UCS GB-06 bắt buộc sai số điện thoại, sai mật khẩu, tài khoản bị khoá và tài khoản
  /// vô hiệu hoá đều không phân biệt được với nhau. Ánh xạ theo MÃ HTTP chứ không theo
  /// câu chữ backend trả — chỉ có một nhánh nên không có gì để lộ.
  static const String signInFailed = 'Số điện thoại hoặc mật khẩu không đúng.';

  /// Dùng riêng cho màn đăng nhập — luôn trả về đúng một câu khi bị từ chối.
  static ApiException forSignIn(Object error) {
    if (error is DioException) {
      final status = error.response?.statusCode;
      if (status == 401) {
        return const ApiException(signInFailed, statusCode: 401);
      }
      if (status == 400) {
        return const ApiException(
          'Vui lòng nhập đầy đủ số điện thoại và mật khẩu.',
          statusCode: 400,
        );
      }
      // Không chạm được backend — ApiErrorMapper.general() xử lý y hệt nhánh này
      // (error.response == null), không cần lặp lại logic.
      if (error.response == null) return ApiErrorMapper.general(error);
    }
    return const ApiException(signInFailed);
  }
}
