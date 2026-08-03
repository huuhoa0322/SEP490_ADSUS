using System.Security.Claims;
using ADSUS_BE.BLL.AIModelManagement.DTOs;
using ADSUS_BE.BLL.AIModelManagement.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

[ApiController]
[Route("api/v1/ai-model-versions")]
[Authorize(Roles = "ADMIN")]
public class AiModelsController : ControllerBase
{
    private readonly IAiModelService _aiModelService;

    public AiModelsController(IAiModelService aiModelService)
    {
        _aiModelService = aiModelService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AiModelVersionDto>>>> SearchVersions(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _aiModelService.SearchVersionsAsync(keyword, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<AiModelVersionDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<AiModelVersionDto>>> GetVersionById(
        Guid id, 
        CancellationToken cancellationToken)
    {
        var version = await _aiModelService.GetVersionByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<AiModelVersionDto>.Ok(version));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<AiModelVersionDto>>> RegisterVersion(
        [FromBody] RegisterModelVersionRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = GetUserId();
        var result = await _aiModelService.RegisterVersionAsync(request, adminId, cancellationToken);
        
        return CreatedAtAction(
            nameof(GetVersionById), 
            new { id = result.ModelVersionId }, 
            ApiResponse<AiModelVersionDto>.Ok(result, "Đăng ký phiên bản AI mới thành công.")
        );
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateVersion(
        Guid id,
        [FromBody] UpdateModelVersionRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = GetUserId();
        await _aiModelService.UpdateVersionAsync(id, request, adminId, cancellationToken);
        
        return Ok(ApiResponse<object>.Ok(new { Message = "Cập nhật phiên bản thành công." }));
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> ActivateVersion(
        Guid id,
        [FromBody] ActivateVersionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Status?.ToUpper() != "ACTIVE")
        {
            throw new BusinessException("Payload không hợp lệ. Vui lòng gửi {\"status\":\"ACTIVE\"}");
        }

        var adminId = GetUserId();
        await _aiModelService.ActivateVersionAsync(id, adminId, cancellationToken);
        
        return Ok(ApiResponse<object>.Ok(new { Message = "Kích hoạt phiên bản thành công." }));
    }

    private Guid GetUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(idClaim) || !Guid.TryParse(idClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Không tìm thấy thông tin định danh của người dùng.");
        }
        return userId;
    }
}

public class ActivateVersionRequest
{
    public string? Status { get; set; }
}
