/**
 * Luật định dạng số điện thoại, khai ở ĐÚNG MỘT CHỖ phía web.
 *
 * Phải khớp với `PhoneNumberRule` bên backend (`ADSUS_BE.BLL/Common/PhoneNumberRule.cs`).
 * Hai bên lệch nhau thì giao diện cho gõ xong mới bị backend đá về — người dùng điền lại
 * form từ đầu mà không hiểu mình sai chỗ nào.
 *
 * Đúng 10 chữ số, bắt đầu bằng 0 (quyết định của nhóm 04/08/2026).
 */
export const PHONE_PATTERN = /^0\d{9}$/;

export const PHONE_MAX_LENGTH = 10;

export const PHONE_ERROR_MESSAGE =
  "Số điện thoại phải bắt đầu bằng 0 và có đúng 10 chữ số.";

export function isValidPhoneNumber(value: string): boolean {
  return PHONE_PATTERN.test(value.trim());
}
