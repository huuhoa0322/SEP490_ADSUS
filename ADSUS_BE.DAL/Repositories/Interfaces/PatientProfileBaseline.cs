using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// UC-06 — khi nào coi là "đã lập hồ sơ nền".
///
/// Không còn đồng nghĩa với "có bản ghi patient_profiles": mọi tài khoản PATIENT đều được
/// tạo sẵn một bản ghi rỗng ngay lúc tạo tài khoản (tự đăng ký, Admin hoặc Điều dưỡng tạo),
/// vì UC-13 chỉ yêu cầu bệnh nhân đã đăng nhập là đặt lịch được — không đợi hồ sơ nền.
///
/// Bản ghi tạo sẵn đứng tên created_by là chính bệnh nhân (hoặc người thân đã thêm họ làm
/// guest). UC-06 ghi đè created_by bằng Bác sĩ/Điều dưỡng lập hồ sơ, nên vai trò của
/// created_by phân biệt được hai trạng thái mà không cần thêm cột mới.
/// </summary>
public static class PatientProfileBaseline
{
    public static bool IsEstablishedBy(UserRole creatorRole) =>
        creatorRole is UserRole.Doctor or UserRole.Staff;
}
