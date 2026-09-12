using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.UserRoleManagement.Validators;

/// <summary>Bước 1 — chỉ kiểm hình dạng số điện thoại. Không kiểm số đó đã có tài khoản hay
/// chưa ở đây (việc đó cần truy vấn DB) — service tự quyết, và báo RÕ 409 nếu đã có tài
/// khoản (xem Global Constraints).</summary>
public class RequestRegistrationOtpRequestValidator : AbstractValidator<RequestRegistrationOtpRequest>
{
    public RequestRegistrationOtpRequestValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(PhoneNumberRule.Pattern).WithMessage(PhoneNumberRule.Message);
    }
}
