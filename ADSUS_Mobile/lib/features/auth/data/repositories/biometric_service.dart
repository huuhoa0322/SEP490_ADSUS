import 'package:flutter/services.dart';
import 'package:local_auth/local_auth.dart';

/// Kết quả một lần xác thực sinh trắc học.
enum BiometricOutcome {
  success,

  /// Quét sai hoặc người dùng huỷ (UC-02 AF-01).
  failed,

  /// Máy không có cảm biến, hoặc chưa đăng ký vân tay/khuôn mặt nào trong hệ điều hành.
  unavailable,
}

/// Bọc thư viện local_auth.
///
/// Mẫu vân tay/khuôn mặt KHÔNG BAO GIỜ đi qua đây. Hệ điều hành giữ mẫu trong secure
/// enclave và chỉ trả về đúng một câu trả lời: đúng hay sai. Ứng dụng không đọc được mẫu,
/// và server lại càng không.
class BiometricService {
  BiometricService({LocalAuthentication? auth})
      : _auth = auth ?? LocalAuthentication();

  final LocalAuthentication _auth;

  /// Máy có dùng được sinh trắc học không (có cảm biến và đã đăng ký ít nhất một mẫu).
  Future<bool> isAvailable() async {
    try {
      if (!await _auth.isDeviceSupported()) return false;
      if (!await _auth.canCheckBiometrics) return false;
      final types = await _auth.getAvailableBiometrics();
      return types.isNotEmpty;
    } on PlatformException {
      return false;
    }
  }

  /// UC-02 bước 2-4 — hiện hộp thoại xác thực của hệ điều hành.
  Future<BiometricOutcome> authenticate() async {
    try {
      final ok = await _auth.authenticate(
        localizedReason: 'Xác thực để đăng nhập vào ADSUS',
        options: const AuthenticationOptions(
          // Chỉ nhận sinh trắc học, không cho lùi về mã PIN của máy — mã PIN máy không
          // chứng minh được người đó là chủ tài khoản ADSUS.
          biometricOnly: true,
          stickyAuth: true,
        ),
      );
      return ok ? BiometricOutcome.success : BiometricOutcome.failed;
    } on PlatformException {
      // Không có cảm biến, chưa đăng ký mẫu, hoặc bị khoá do quét sai quá nhiều lần.
      // UC-02 BR-02: mọi trường hợp đều cho phép lùi về đăng nhập bằng mật khẩu.
      return BiometricOutcome.unavailable;
    }
  }
}
