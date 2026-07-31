/// Cấu hình địa chỉ backend.
class ApiConstants {
  const ApiConstants._();

  /// Địa chỉ backend ADSUS_BE.
  ///
  /// Truyền lúc build để mỗi người dùng địa chỉ của mình mà không phải sửa code:
  ///   flutter run --dart-define=API_BASE_URL=http://192.168.1.10:5036
  ///
  /// Mặc định dùng 10.0.2.2 — đó là cách máy ảo Android gọi về localhost của máy tính.
  /// Chạy trên điện thoại thật thì PHẢI truyền IP thật của máy tính trong mạng LAN,
  /// vì với điện thoại thì "localhost" là chính nó, không phải máy tính.
  ///
  /// BẢN RELEASE PHẢI TRUYỀN ĐỊA CHỈ https. Android 9 trở lên chặn HTTP không mã hoá theo
  /// mặc định, mà chỉ bản debug mới bật usesCleartextTraffic. Nên giá trị mặc định http ở
  /// đây chỉ dùng được lúc phát triển:
  ///   flutter build apk --release --dart-define=API_BASE_URL=https://api.adsus...
  static const String baseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5036',
  );

  static const String login = '/api/v1/auth/login';
  static const String forgotPassword = '/api/v1/auth/forgot-password';
  static const String changePassword = '/api/v1/auth/change-password';
  static const String myProfile = '/api/v1/users/me';
  static const String myBiometric = '/api/v1/users/me/biometric';

  /// Quá thời gian này coi như không kết nối được.
  static const Duration timeout = Duration(seconds: 15);
}
