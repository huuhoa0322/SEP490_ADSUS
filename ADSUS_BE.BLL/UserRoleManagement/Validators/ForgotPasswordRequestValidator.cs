using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.UserRoleManagement.Validators;

/// <summary>
/// UC-03 — chỉ kiểm HÌNH DẠNG dữ liệu.
///
/// Cố ý KHÔNG kiểm số điện thoại hay email có tồn tại không: trả lời câu đó chính là làm lộ
/// tài khoản nào có thật (AF-01). Việc đối chiếu nằm ở service và im lặng.
/// </summary>
public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        // Cùng luật định dạng với màn tạo tài khoản (PhoneNumberRule).
        //
        // Kiểm định dạng ở đây KHÔNG vi phạm AF-01: nó chỉ nói "chuỗi này không thể là số
        // điện thoại", đúng với mọi giá trị sai dạng, chứ không hé lộ số đó có tài khoản hay
        // không. Trước đây thiếu, nên gõ thiếu một chữ số là lời gọi vẫn xuống tới database
        // rồi im lặng không làm gì — người dùng ngồi chờ mail mãi không tới mà không hiểu vì
        // sao, trong khi thực ra chỉ gõ nhầm.
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(PhoneNumberRule.Pattern).WithMessage(PhoneNumberRule.Message);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not a valid address.")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters.");
    }
}
