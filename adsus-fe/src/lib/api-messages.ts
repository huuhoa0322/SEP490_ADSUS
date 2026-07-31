/**
 * Dịch thông báo lỗi của backend sang tiếng Việt.
 *
 * Vì sao dịch ở đây mà không sửa thẳng backend: cùng một API còn phục vụ ứng dụng di động,
 * và các mã lỗi tiếng Anh còn đi vào log lẫn tài liệu kiểm thử. Giữ backend nói một thứ
 * tiếng, rồi mỗi client tự dịch cho người dùng của mình — giống cách ROLE_LABEL đang dịch
 * "DOCTOR" thành "Bác sĩ".
 *
 * Thiếu một câu trong bảng này thì hiện nguyên văn tiếng Anh, chứ không hiện chuỗi rỗng —
 * xấu nhưng vẫn đọc hiểu được, còn hơn không nói gì.
 */
const MESSAGES: Record<string, string> = {
  // ---- Đăng nhập và mật khẩu (UC-01, UC-25) ----
  "Invalid phone number or password.":
    "Số điện thoại hoặc mật khẩu không đúng.",
  "Too many requests. Please wait before trying again.":
    "Bạn đã gửi quá nhiều yêu cầu. Vui lòng chờ một lúc rồi thử lại.",
  "Invalid access token.": "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.",
  "Current password is incorrect.": "Mật khẩu hiện tại không đúng.",
  "This account is no longer active.": "Tài khoản này không còn hiệu lực.",
  "Password is required.": "Vui lòng nhập mật khẩu.",
  "Current password is required.": "Vui lòng nhập mật khẩu hiện tại.",
  "New password is required.": "Vui lòng nhập mật khẩu mới.",
  "Password confirmation is required.": "Vui lòng nhập lại mật khẩu mới.",
  "Password confirmation does not match the new password.":
    "Mật khẩu nhập lại không khớp với mật khẩu mới.",
  "New password must contain at least one uppercase letter.":
    "Mật khẩu mới phải có ít nhất một chữ hoa.",
  "New password must contain at least one digit.":
    "Mật khẩu mới phải có ít nhất một chữ số.",
  "If the information is correct, a new password has been sent to your email.":
    "Nếu thông tin chính xác, mật khẩu mới đã được gửi tới email của bạn.",

  // ---- Dữ liệu nhập vào (UC-04 FT-07/FT-09) ----
  "Phone number is required.": "Vui lòng nhập số điện thoại.",
  "Phone number must not exceed 15 characters.":
    "Số điện thoại không được quá 15 ký tự.",
  "Phone number must start with 0 and contain 9 to 11 digits.":
    "Số điện thoại phải bắt đầu bằng 0 và có 9 đến 11 chữ số.",
  "Full name is required.": "Vui lòng nhập họ và tên.",
  "Full name must not exceed 100 characters.": "Họ và tên không được quá 100 ký tự.",
  "Role is required.": "Vui lòng chọn vai trò.",
  "Role must be one of DOCTOR, NURSE or PATIENT.":
    "Vai trò chỉ được là Bác sĩ, Điều dưỡng hoặc Bệnh nhân.",
  "Role must be one of ADMIN, DOCTOR, NURSE or PATIENT.":
    "Vai trò không hợp lệ.",
  "Email is required.": "Vui lòng nhập email.",
  "Email is not a valid address.": "Email không đúng định dạng.",
  "Email format is invalid.": "Email không đúng định dạng.",
  "Email must not exceed 255 characters.": "Email không được quá 255 ký tự.",
  "Date of birth must be in yyyy-MM-dd format and must not be in the future.":
    "Ngày sinh phải đúng định dạng và không được ở tương lai.",
  "Date of birth must be in yyyy-MM-dd format.": "Ngày sinh không đúng định dạng.",
  "Date of birth must not be in the future.": "Ngày sinh không được ở tương lai.",

  // ---- Quản lý tài khoản (UC-04) ----
  "Account not found.": "Không tìm thấy tài khoản.",
  "This phone number is already used by another account.":
    "Số điện thoại này đã có tài khoản khác dùng.",
  "This email is already used by another account.":
    "Email này đã có tài khoản khác dùng.",
  "You cannot lock or deactivate your own account.":
    "Bạn không thể khoá hoặc vô hiệu hoá chính tài khoản của mình.",
  "This account has been deactivated and cannot be changed.":
    "Tài khoản này đã bị vô hiệu hoá nên không thay đổi được nữa.",
  "This account has no email address, so a temporary password cannot be delivered.":
    "Tài khoản này chưa có email nên không gửi được mật khẩu tạm. Hãy bổ sung email trước.",
  "An administrator's role cannot be changed here, and no account can be promoted to administrator on this screen.":
    "Không đổi được vai trò của quản trị viên, và cũng không phong quản trị viên ở màn hình này.",

  // ---- Kết quả có hậu quả kèm theo ----
  "Account created. A temporary password has been emailed.":
    "Đã tạo tài khoản và gửi mật khẩu tạm qua email.",
  "Account created, but it has no email address so no temporary password could be delivered. Add an email address, then use Reset password.":
    "Đã tạo tài khoản, nhưng chưa có email nên không gửi được mật khẩu tạm. Hãy bổ sung email rồi bấm Cấp lại mật khẩu.",
  "Account created, but the temporary password could not be emailed. Use Reset password to try sending it again.":
    "Đã tạo tài khoản, nhưng không gửi được email chứa mật khẩu tạm. Hãy bấm Cấp lại mật khẩu để gửi lại.",
  "The temporary password could not be emailed, so the current password was left unchanged. Please try again later.":
    "Không gửi được email nên mật khẩu hiện tại được giữ nguyên. Vui lòng thử lại sau.",
  "Account updated.": "Đã lưu thay đổi.",
  "Account locked.": "Đã khoá tài khoản.",
  "Account unlocked.": "Đã mở khoá tài khoản.",
  "Account deactivated permanently.": "Đã vô hiệu hoá tài khoản vĩnh viễn.",
  "A temporary password has been emailed to the account holder.":
    "Đã gửi mật khẩu tạm tới email của chủ tài khoản.",
  "Password changed successfully.": "Đã đổi mật khẩu.",
  "Profile updated successfully.": "Đã cập nhật hồ sơ.",
  "Operation failed.": "Thao tác không thành công.",
};

/**
 * Backend nối nhiều lỗi kiểm tra dữ liệu bằng dấu cách (`string.Join(" ", ...)`), nên chuỗi
 * nhận về có thể là vài câu dính liền. Tách theo dấu chấm rồi dịch từng câu, sau đó ghép
 * lại — dịch cả cụm thì chỉ cần lệch một câu là hỏng toàn bộ.
 */
export function translateApiMessage(message: string): string {
  const exact = MESSAGES[message.trim()];
  if (exact) return exact;

  const parts = message.match(/[^.]+\./g);
  if (!parts || parts.length < 2) return message;

  return parts
    .map((part) => MESSAGES[part.trim()] ?? part.trim())
    .join(" ");
}
