using ADSUS_BE.BLL.ClinicServiceManagement;
using ADSUS_BE.BLL.ClinicServiceManagement.DTOs;
using ADSUS_BE.BLL.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

[ApiController]
[Route("api/v1/clinic-services")]
[Produces("application/json")]
[Authorize]
public class ClinicServicesController : ControllerBase
{
    private readonly IClinicServiceManagementService _clinicServiceManagementService;

    public ClinicServicesController(IClinicServiceManagementService clinicServiceManagementService)
    {
        _clinicServiceManagementService = clinicServiceManagementService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ClinicServiceResponse>>>> GetAll(
        [FromQuery] bool? isActive,
        CancellationToken ct)
    {
        var services = await _clinicServiceManagementService.GetAllAsync(isActive, ct);
        return Ok(ApiResponse<IReadOnlyList<ClinicServiceResponse>>.Ok(services));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<ClinicServiceResponse>>> GetById(
        Guid id,
        CancellationToken ct)
    {
        var service = await _clinicServiceManagementService.GetByIdAsync(id, ct);
        return Ok(ApiResponse<ClinicServiceResponse>.Ok(service));
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<ClinicServiceResponse>>> Create(
        [FromBody] CreateClinicServiceRequest request,
        CancellationToken ct)
    {
        var result = await _clinicServiceManagementService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<ClinicServiceResponse>.Ok(result, "Tạo dịch vụ thành công.", 201));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<ClinicServiceResponse>>> Update(
        Guid id,
        [FromBody] UpdateClinicServiceRequest request,
        CancellationToken ct)
    {
        var result = await _clinicServiceManagementService.UpdateAsync(id, request, ct);
        return Ok(ApiResponse<ClinicServiceResponse>.Ok(result, "Cập nhật dịch vụ thành công."));
    }

    [HttpPut("{id:guid}/deactivate")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<object?>>> Deactivate(
        Guid id,
        CancellationToken ct)
    {
        await _clinicServiceManagementService.DeactivateAsync(id, ct);
        return Ok(ApiResponse<object?>.Ok(null, "Vô hiệu hóa dịch vụ thành công."));
    }
}
