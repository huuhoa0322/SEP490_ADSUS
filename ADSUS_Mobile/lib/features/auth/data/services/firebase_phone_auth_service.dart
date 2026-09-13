import 'package:firebase_auth/firebase_auth.dart';

/// Bọc Firebase Phone Auth SDK — nơi DUY NHẤT trong Mobile app gọi trực tiếp
/// `FirebaseAuth.instance` cho việc xác thực số điện thoại. Chuyển đổi định dạng số điện
/// thoại nội địa (0xxxxxxxxx, dùng xuyên suốt phần còn lại của app) sang E.164 (+84xxxxxxxxx,
/// Firebase bắt buộc) ngay tại đây — biên giới duy nhất cho việc đổi định dạng phía Mobile.
class FirebasePhoneAuthService {
  const FirebasePhoneAuthService();

  static const String _vietnamCountryCodePrefix = '+84';

  /// Gửi mã OTP qua Firebase — không trả về Future vì Firebase dùng callback, không phải
  /// request/response đơn giản (có thể tự động xác thực trên máy Android hỗ trợ, khi đó
  /// `onCodeSent` không được gọi và luồng UI cần xử lý ở tầng gọi nếu muốn hỗ trợ auto-retrieval;
  /// plan này chỉ dùng đường nhập tay mã, không cần `verificationCompleted` tự động).
  Future<void> sendCode({
    required String localPhoneNumber,
    required void Function(String verificationId) onCodeSent,
    required void Function(String message) onFailed,
  }) async {
    await FirebaseAuth.instance.verifyPhoneNumber(
      phoneNumber: _toE164(localPhoneNumber),
      timeout: const Duration(seconds: 60),
      verificationCompleted: (_) {
        // Auto-retrieval trên một số máy Android — bỏ qua có chủ đích, luồng UI của plan này
        // luôn để người dùng tự nhập mã (nhất quán trên mọi thiết bị, không phân nhánh UI).
      },
      verificationFailed: (FirebaseAuthException e) {
        onFailed(_mapErrorMessage(e));
      },
      codeSent: (String verificationId, int? resendToken) {
        onCodeSent(verificationId);
      },
      codeAutoRetrievalTimeout: (String verificationId) {},
    );
  }

  /// Xác thực mã người dùng nhập, trả về Firebase ID Token để gửi lên backend.
  Future<String> confirmCode({
    required String verificationId,
    required String smsCode,
  }) async {
    final credential = PhoneAuthProvider.credential(
      verificationId: verificationId,
      smsCode: smsCode,
    );

    final userCredential = await FirebaseAuth.instance.signInWithCredential(credential);
    final idToken = await userCredential.user!.getIdToken();

    // Đăng xuất khỏi Firebase NGAY sau khi lấy token — phiên đăng nhập thật của app nằm ở
    // access token backend (lưu trong flutter_secure_storage), không phải phiên Firebase.
    // Giữ phiên Firebase tồn tại sau khi dùng xong dễ gây nhầm lẫn 2 khái niệm "đã đăng nhập".
    await FirebaseAuth.instance.signOut();

    return idToken!;
  }

  String _toE164(String localPhoneNumber) =>
      '$_vietnamCountryCodePrefix${localPhoneNumber.substring(1)}';

  String _mapErrorMessage(FirebaseAuthException e) {
    switch (e.code) {
      case 'invalid-phone-number':
        return 'Số điện thoại không hợp lệ.';
      case 'too-many-requests':
        return 'Bạn đã thử quá nhiều lần. Vui lòng thử lại sau.';
      case 'invalid-verification-code':
        return 'Mã xác thực không đúng.';
      case 'session-expired':
        return 'Mã xác thực đã hết hạn. Vui lòng gửi lại mã.';
      default:
        return 'Không xác thực được số điện thoại. Vui lòng thử lại.';
    }
  }
}
