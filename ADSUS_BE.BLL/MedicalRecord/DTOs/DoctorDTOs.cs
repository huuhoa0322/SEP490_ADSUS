namespace ADSUS_BE.BLL.MedicalRecord.DTOs;

/// <summary>
/// Một dòng trong ô chọn "Bác sĩ phụ trách" khi tạo ca khám (GB-04, UC-07 bước 5).
///
/// CỐ Ý chỉ có id và họ tên. Không email, không trạng thái tài khoản, không ngày sinh —
/// đó là dữ liệu quản trị tài khoản của Module 2, không thuộc góc nhìn lâm sàng này. Cùng
/// lý do đã tách /api/v1/patients khỏi /api/v1/admin/users.
/// </summary>
public sealed record DoctorSummaryResponse(Guid UserId, string FullName);
