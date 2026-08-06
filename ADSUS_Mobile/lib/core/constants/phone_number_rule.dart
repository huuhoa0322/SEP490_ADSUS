/// Luật định dạng số điện thoại, khai ở ĐÚNG MỘT CHỖ phía mobile.
///
/// Phải khớp với `PhoneNumberRule` bên backend
/// (`ADSUS_BE.BLL/Common/PhoneNumberRule.cs`) và `phone-number.ts` bên web. Ba nơi lệch nhau
/// thì app cho gõ xong mới bị backend đá về, người dùng điền lại từ đầu mà không hiểu sai chỗ
/// nào.
///
/// Đúng 10 chữ số, bắt đầu bằng 0 (quyết định của nhóm 04/08/2026).
class PhoneNumberRule {
  const PhoneNumberRule._();

  static final RegExp pattern = RegExp(r'^0\d{9}$');

  static const int length = 10;

  static const String errorMessage =
      'Số điện thoại phải bắt đầu bằng 0 và có đúng 10 chữ số.';

  static bool isValid(String value) => pattern.hasMatch(value.trim());
}
