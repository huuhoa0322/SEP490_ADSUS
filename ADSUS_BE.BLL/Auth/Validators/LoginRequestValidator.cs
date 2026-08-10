using ADSUS_BE.BLL.Auth.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.Auth.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        // Input shape only. Whether the phone number exists or the password is correct is
        // NOT checked here — answering that in detail would violate GB-06.
        //
        // CỐ Ý KHÔNG kiểm định dạng số điện thoại ở đây, khác với màn tạo tài khoản và màn
        // quên mật khẩu (PhoneNumberRule). Đây là chỗ khác biệt có chủ đích, không phải bỏ sót:
        //
        //   1. Đăng nhập là ĐỐI CHIẾU, không phải nhập liệu. Số sai định dạng thì đằng nào
        //      cũng không khớp tài khoản nào — để nó trả về đúng một câu của GB-06 là xong,
        //      thêm một loại thông báo thứ hai chỉ làm màn đăng nhập nói nhiều hơn cần thiết.
        //   2. Siết định dạng ở đây là NHỐT NGƯỜI DÙNG RA NGOÀI. Nếu trong database còn tài
        //      khoản cũ có số 9 hay 11 chữ số, họ sẽ không tài nào đăng nhập được nữa, kể cả
        //      khi mật khẩu vẫn đúng — mà lỗi kiểu đó rất khó lần ra.
        //
        // Hai màn kia thì ngược lại: người dùng đang GÕ MỚI một số, bắt lỗi sớm là giúp họ.
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .MaximumLength(15).WithMessage("Phone number must not exceed 15 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
