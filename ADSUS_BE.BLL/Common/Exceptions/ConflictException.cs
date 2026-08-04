namespace ADSUS_BE.BLL.Common.Exceptions;

/// <summary>
/// Thrown when the request clashes with an existing record — e.g. creating a second Patient
/// Profile for the same account (unique constraint uq_patient_profiles_user).
/// Mapped to HTTP 409 by GlobalExceptionHandler.
///
/// Tách khỏi BusinessException (422) có chủ đích: 422 nghĩa là dữ liệu gửi lên vi phạm quy
/// tắc nghiệp vụ, còn 409 nghĩa là dữ liệu hợp lệ nhưng trạng thái hiện tại của hệ thống
/// không cho phép — client xử lý hai trường hợp này khác nhau.
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
