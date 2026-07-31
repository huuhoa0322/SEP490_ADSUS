/// Khoá dùng trong flutter_secure_storage.
///
/// Đây là vùng lưu được hệ điều hành mã hoá (Keystore trên Android, Keychain trên iOS),
/// nên token nằm ở đây an toàn hơn hẳn so với SharedPreferences.
class StorageKeys {
  const StorageKeys._();

  /// Access token JWT.
  static const String accessToken = 'adsus.accessToken';

  /// Số điện thoại của lần đăng nhập gần nhất.
  ///
  /// UC-02 BR-01: sinh trắc học chỉ dùng được sau khi đã đăng nhập bằng mật khẩu thành
  /// công ít nhất một lần trên máy này. Có khoá này nghĩa là điều kiện đó đã thoả.
  static const String pairedPhone = 'adsus.pairedPhone';

  /// Người dùng đã bật đăng nhập sinh trắc học trên chính máy này hay chưa.
  static const String biometricEnabled = 'adsus.biometricEnabled';
}
