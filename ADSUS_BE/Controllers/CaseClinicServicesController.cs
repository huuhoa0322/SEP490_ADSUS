using ADSUS_BE.BLL.CaseClinicServices;
using ADSUS_BE.BLL.CaseClinicServices.DTOs;
using ADSUS_BE.BLL.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

[ApiController]
[Route("api/v1/cases/{caseId:guid}/services")]
[Produces("application/json")]
public class CaseClinicServicesController : ControllerBase
{
    private readonly ICaseClinicServiceService _caseClinicServiceService;

    public CaseClinicServicesController(ICaseClinicServiceService caseClinicServiceService)
    {
        _caseClinicServiceService = caseClinicServiceService;
    }

    [HttpGet]
    [Authorize(Roles = "DOCTOR,STAFF")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CaseClinicServiceResponse>>>> GetServicesForCase(
        Guid caseId,
        CancellationToken ct)
    {
        var services = await _caseClinicServiceService.GetServicesForCaseAsync(caseId, ct);
        return Ok(ApiResponse<IReadOnlyList<CaseClinicServiceResponse>>.Ok(services));
    }

    [HttpPost]
    [Authorize(Roles = "DOCTOR")]
    public async Task<ActionResult<ApiResponse<CaseClinicServiceResponse>>> AddServiceToCase(
        Guid caseId,
        [FromBody] AddCaseClinicServiceRequest request,
        CancellationToken ct)
    {
        var result = await _caseClinicServiceService.AddServiceToCaseAsync(caseId, request.ClinicServiceId, ct);
        return Ok(ApiResponse<CaseClinicServiceResponse>.Ok(result, "Thêm dịch vụ thành công."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "DOCTOR")]
    public async Task<ActionResult<ApiResponse<object?>>> RemoveServiceFromCase(
        Guid caseId,
        Guid id,
        CancellationToken ct)
    {
        await _caseClinicServiceService.RemoveServiceFromCaseAsync(caseId, id, ct);
        return Ok(ApiResponse<object?>.Ok(null, "Xóa dịch vụ thành công."));
    }
}
