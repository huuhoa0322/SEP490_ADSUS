using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

/// <summary>
/// UC-07 GB-04 — danh sách Bác sĩ để chọn người phụ trách khi tạo ca khám (Web SCR-11).
///
/// Endpoint này NẰM NGOÀI ADSUS_API_Catalog_v1.1 — thêm mới vì GB-04 bắt buộc mọi ca khám
/// phải gắn đúng một Bác sĩ chịu trách nhiệm, và Điều dưỡng tạo ca hộ thì phải chọn được
/// người đó (UC-07 bước 5), trong khi không endpoint nào có sẵn cho phép Doctor/Nurse tra
/// danh sách Bác sĩ. Đã ghi vào Flags Summary và Catalog v1.2.
/// </summary>
[ApiController]
[Route("api/v1/doctors")]
[Authorize(Roles = "DOCTOR,NURSE")]
[Produces("application/json")]
public sealed class DoctorsController : ControllerBase
{
    private readonly IDoctorDirectoryService _doctors;

    public DoctorsController(IDoctorDirectoryService doctors) => _doctors = doctors;

    /// <summary>Bác sĩ đang hoạt động, sắp theo họ tên. Không phân trang.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DoctorSummaryResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _doctors.ListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<DoctorSummaryResponse>>.Ok(result, "Doctors retrieved successfully"));
    }
}
