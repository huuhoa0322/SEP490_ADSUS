using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.PatientRelationship.DTOs;
using ADSUS_BE.BLL.PatientRelationship.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.Controllers;

[ApiController]
[Route("api/v1/relatives")]
[Authorize(Roles = "PATIENT")]
public sealed class PatientRelationshipsController : ControllerBase
{
    private readonly IPatientRelationshipService _service;
    private readonly AppDbContext _db;

    public PatientRelationshipsController(
        IPatientRelationshipService service,
        AppDbContext db)
    {
        _service = service;
        _db = db;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdClaim!);
    }

    /// <summary>
    /// Lấy danh sách người thân đã lưu.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<RelativesListResponse>> GetRelatives(CancellationToken ct)
    {
        var result = await _service.GetRelativesAsync(GetCurrentUserId(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Lấy chi tiết một người thân.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RelativeResponse>> GetRelative(Guid id, CancellationToken ct)
    {
        var result = await _service.GetRelativeByIdAsync(id, GetCurrentUserId(), ct);
        if (result == null) return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// Thêm người thân mới.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<RelativeResponse>> AddRelative(
        [FromBody] AddRelativeRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _service.AddRelativeAsync(request, GetCurrentUserId(), ct);
            return CreatedAtAction(nameof(GetRelative), new { id = result.RelationshipId }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Cập nhật người thân.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RelativeResponse>> UpdateRelative(
        Guid id,
        [FromBody] UpdateRelativeRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _service.UpdateRelativeAsync(id, request, GetCurrentUserId(), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Xóa người thân.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteRelative(Guid id, CancellationToken ct)
    {
        try
        {
            await _service.DeleteRelativeAsync(id, GetCurrentUserId(), ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Kiểm tra SĐT đã có tài khoản chưa.
    /// </summary>
    [HttpGet("check-phone")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> CheckPhone([FromQuery] string phone, CancellationToken ct)
    {
        var exists = await _service.IsPhoneRegisteredAsync(phone, ct);
        return Ok(new { phone, isRegistered = exists });
    }

    /// <summary>
    /// POST /api/v1/relatives/guardian/{guardianUserId} — Staff tạo người thân cho mẹ
    /// </summary>
    [HttpPost("guardian/{guardianUserId:guid}")]
    [Authorize(Roles = "STAFF,ADMIN,RECEPTIONIST")]
    public async Task<IActionResult> AddRelativeForGuardian(
        Guid guardianUserId,
        [FromBody] AddRelativeRequest request,
        CancellationToken ct = default)
    {
        var guardian = await _db.Users.FirstOrDefaultAsync(
            u => u.UserId == guardianUserId && u.Role == UserRole.Patient, ct);
        if (guardian == null)
            return NotFound(ApiResponse<object>.Fail(404, "Không tìm thấy tài khoản bệnh nhân."));

        var result = await _service.AddRelativeAsync(request, guardianUserId, ct);
        return Ok(ApiResponse<RelativeResponse>.Ok(result));
    }

    /// <summary>
    /// GET /api/v1/relatives/guardian/{guardianUserId} — Staff lấy danh sách người thân của mẹ
    /// </summary>
    [HttpGet("guardian/{guardianUserId:guid}")]
    [Authorize(Roles = "STAFF,ADMIN,RECEPTIONIST")]
    public async Task<IActionResult> GetRelativesForGuardian(
        Guid guardianUserId,
        CancellationToken ct = default)
    {
        var relatives = await _service.GetRelativesAsync(guardianUserId, ct);
        return Ok(ApiResponse<RelativesListResponse>.Ok(relatives));
    }
}
