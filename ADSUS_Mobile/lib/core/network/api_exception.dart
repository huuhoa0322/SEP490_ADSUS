import 'package:dio/dio.dart';

/// Lỗi khi gọi API, đã được dịch sang tiếng Việt để hiển thị thẳng cho người dùng.
class ApiException implements Exception {
  const ApiException(this.message, {this.statusCode});

  final String message;
  final int? statusCode;

  @override
  String toString() => message;
}

/// Chuyển lỗi của Dio thành thông báo tiếng Việt.
///
/// Backend trả tiếng Anh theo api_design_rules và dùng chung với web, nên việc dịch
/// thuộc về bên hiển thị. Người dùng ứng dụng này là bệnh nhân Việt Nam.
class ApiErrorMapper {
  const ApiErrorMapper._();

  /// Câu DUY NHẤT hiển thị cho mọi trường hợp đăng nhập thất bại.
  ///
  /// UCS GB-06 bắt buộc sai số điện thoại, sai mật khẩu, tài khoản bị khoá và tài khoản
  /// vô hiệu hoá đều không phân biệt được với nhau. Ánh xạ theo MÃ HTTP chứ không theo
  /// câu chữ backend trả — chỉ có một nhánh nên không có gì để lộ.
  static const String signInFailed = 'Số điện thoại hoặc mật khẩu không đúng.';

  static const Map<String, String> _known = {
    'Current password is incorrect.': 'Mật khẩu hiện tại không đúng.',
    'This account is no longer active.': 'Tài khoản này đã ngừng hoạt động.',
    'Invalid access token.': 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.',
    'This email is already used by another account.':
        'Email này đã được tài khoản khác sử dụng.',
    'New password must be at least 8 characters.':
        'Mật khẩu mới phải có ít nhất 8 ký tự.',
    'New password must contain at least one uppercase letter.':
        'Mật khẩu mới phải có ít nhất 1 chữ hoa.',
    'New password must contain at least one digit.':
        'Mật khẩu mới phải có ít nhất 1 chữ số.',
    'Password confirmation does not match the new password.':
        'Xác nhận mật khẩu không khớp với mật khẩu mới.',
    'Full name is required.': 'Vui lòng nhập họ tên.',
    'Email format is invalid.': 'Email không đúng định dạng.',
    'Date of birth must not be in the future.':
        'Ngày sinh không được ở tương lai.',
    'Date of birth must be in yyyy-MM-dd format.':
        'Ngày sinh không đúng định dạng.',
  };

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
      if (error.response == null) return ApiException(_noConnection());
    }
    return const ApiException(signInFailed);
  }

  /// Dùng cho các màn khác — ở đó được phép nói rõ nguyên nhân vì người dùng đã đăng nhập.
  static ApiException general(Object error, {String fallback = 'Đã có lỗi xảy ra.'}) {
    if (error is DioException) {
      if (error.response == null) return ApiException(_noConnection());

      final raw = _messageFromBody(error.response?.data);
      if (raw != null) {
        for (final entry in _known.entries) {
          if (raw.contains(entry.key)) {
            return ApiException(entry.value, statusCode: error.response?.statusCode);
          }
        }
        return ApiException(raw, statusCode: error.response?.statusCode);
      }
    }
    return ApiException(fallback);
  }

  static String? _messageFromBody(dynamic data) {
    if (data is Map && data['message'] is String) return data['message'] as String;
    return null;
  }

  static String _noConnection() =>
      'Không kết nối được tới máy chủ. Kiểm tra mạng và xem backend đã chạy chưa.';
}
