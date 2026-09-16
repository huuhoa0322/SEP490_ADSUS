/// Khoá dùng trong flutter_secure_storage.
///
/// Đây là vùng lưu được hệ điều hành mã hoá (Keystore trên Android, Keychain trên iOS),
/// nên token nằm ở đây an toàn hơn hẳn so với SharedPreferences.
class StorageKeys {
  const StorageKeys._();

  /// Access token JWT.
  static const String accessToken = 'adsus.accessToken';

  /// Số điện thoại đang đăng nhập trên máy này — dùng để phân biệt cache dữ liệu cục bộ
  /// (ví dụ reminder preferences) giữa các tài khoản.
  static const String pairedPhone = 'adsus.pairedPhone';
}
