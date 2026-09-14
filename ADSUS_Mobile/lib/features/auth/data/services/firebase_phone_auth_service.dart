import 'dart:async';

import 'package:firebase_auth/firebase_auth.dart';

import '../../../../core/network/api_exception.dart';

/// Bọc Firebase Phone Auth SDK — nơi DUY NHẤT trong Mobile app gọi trực tiếp
/// `FirebaseAuth.instance` cho việc xác thực số điện thoại. Chuyển đổi định dạng số điện
/// thoại nội địa (0xxxxxxxxx, dùng xuyên suốt phần còn lại của app) sang E.164 (+84xxxxxxxxx,
/// Firebase bắt buộc) ngay tại đây — biên giới duy nhất cho việc đổi định dạng phía Mobile.
class FirebasePhoneAuthService {
  const FirebasePhoneAuthService();

  static const String _vietnamCountryCodePrefix = '+84';

  /// Gửi mã OTP qua Firebase, trả về `verificationId` khi mã đã gửi thành công.
  ///
  /// `FirebaseAuth.verifyPhoneNumber` tự nó chỉ hoàn tất sau khi đăng ký xong 1 listener trên
  /// event channel — KHÔNG đợi tới khi `codeSent`/`verificationFailed` thực sự xảy ra (2 sự kiện
  /// đó tới sau, độc lập, trên chính channel đó). Vì vậy phải tự bắc cầu qua `Completer` để hàm
  /// này thực sự chờ đúng kết quả, thay vì trả về ngay khi listener vừa đăng ký xong.
  Future<String> sendCode({required String localPhoneNumber}) {
    final completer = Completer<String>();

    FirebaseAuth.instance
        .verifyPhoneNumber(
      phoneNumber: _toE164(localPhoneNumber),
      timeout: const Duration(seconds: 60),
      verificationCompleted: (_) {
        // Auto-retrieval trên một số máy Android — bỏ qua có chủ đích, luồng UI của plan này
        // luôn để người dùng tự nhập mã (nhất quán trên mọi thiết bị, không phân nhánh UI).
      },
      verificationFailed: (FirebaseAuthException e) {
        if (!completer.isCompleted) {
          completer.completeError(ApiException(_mapErrorMessage(e)));
        }
      },
      codeSent: (String verificationId, int? resendToken) {
        if (!completer.isCompleted) completer.complete(verificationId);
      },
      codeAutoRetrievalTimeout: (String verificationId) {
        // Thường tới SAU `codeSent` (guard `isCompleted` chặn hoàn tất Completer 2 lần) — nhưng
        // nếu máy tự động xác thực và `codeSent` chưa từng tới, đây là cách duy nhất Future này
        // còn hoàn tất được, tránh treo vĩnh viễn.
        if (!completer.isCompleted) completer.complete(verificationId);
      },
    )
        .catchError((Object error) {
      // Lỗi đồng bộ/ngoài giao thức callback (vd. platform không hỗ trợ) — không đi qua
      // verificationFailed, nên phải tự bắt ở đây để Future không bao giờ treo hay throw
      // ra ngoài dạng chưa dịch tiếng Việt.
      if (!completer.isCompleted) {
        completer.completeError(
          const ApiException('Không xác thực được số điện thoại. Vui lòng thử lại.'),
        );
      }
    });

    return completer.future;
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
