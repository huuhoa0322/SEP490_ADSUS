using System.Security.Claims;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

/// <summary>
/// UC-07, UC-08, UC-12 — ca khám và ảnh siêu âm.
/// Bác sĩ/Điều dưỡng xem bản đầy đủ trên Web (SCR-12); Bệnh nhân xem bản đã duyệt trên
/// Mobile (SCR-13/14).
/// </summary>
[ApiController]
[Route("api/v1/cases")]
[Authorize]
[Produces("application/json")]
public sealed class CasesController : ControllerBase
{
    private readonly ICaseService _cases;
    private readonly System.Lazy<ICaseReportService> _reportsLazy;
    private readonly IPrescriptionService _prescriptions;
    private readonly IAppointmentService _appointmentService;
    private readonly IValidator<CreateCaseRequest> _createValidator;
    private readonly IValidator<CaseConclusionRequest> _conclusionValidator;

    public CasesController(
        ICaseService cases,
        System.Lazy<ICaseReportService> reportsLazy,
        IPrescriptionService prescriptions,
        IAppointmentService appointmentService,
        IValidator<CreateCaseRequest> createValidator,
        IValidator<CaseConclusionRequest> conclusionValidator)
    {
        _cases = cases;
        _reportsLazy = reportsLazy;
        _prescriptions = prescriptions;
        _appointmentService = appointmentService;
        _createValidator = createValidator;
        _conclusionValidator = conclusionValidator;
    }

    /// <summary>Danh sách ảnh siêu âm thô của một ca (UC-07, UC-08).</summary>
    [HttpGet("{caseId:guid}/ultrasound-images")]
    [Authorize(Roles = "DOCTOR,NURSE")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UltrasoundImageResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListImages(Guid caseId, CancellationToken ct)
    {
        var result = await _cases.ListImagesAsync(caseId, ct);
        return Ok(ApiResponse<IReadOnlyList<UltrasoundImageResponse>>.Ok(result));
    }

    /// <summary>
    /// Danh sách lần khám của một bệnh nhân, cho Bác sĩ/Điều dưỡng (Web SCR-12) (UC-08).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "DOCTOR,NURSE")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<StaffCaseSummaryResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListByPatient(
        [FromQuery] Guid patientProfileId,
        [FromQuery] string? status,
        [FromQuery] string sortOrder = "desc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (patientProfileId == Guid.Empty)
        {
            return BadRequest(ApiResponse<object>.Fail(
                StatusCodes.Status400BadRequest, "patientProfileId is required."));
        }

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var result = await _cases.ListByPatientProfileAsync(
            patientProfileId, status, sortOrder, page, pageSize, ct);

        return Ok(ApiResponse<PagedResult<StaffCaseSummaryResponse>>.Ok(result, "Cases retrieved successfully"));
    }

    /// <summary>
    /// Danh sách lần khám của chính bệnh nhân đang đăng nhập (Mobile SCR-13) (UC-08).
    /// Luôn chỉ trả về ca đã CONFIRMED — không có tham số trạng thái.
    /// </summary>
    [HttpGet("me")]
    [Authorize(Roles = "PATIENT")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CaseSummaryResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListMine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var result = await _cases.ListMineAsync(GetCallerUserId(), page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<CaseSummaryResponse>>.Ok(result, "Cases retrieved successfully"));
    }

    /// <summary>
    /// Chi tiết một lần khám (UC-08).
    ///
    /// Hình dạng dữ liệu trả về KHÁC NHAU theo vai trò: Bác sĩ/Điều dưỡng nhận
    /// <see cref="CaseResponse"/> đầy đủ; Bệnh nhân nhận <see cref="PatientCaseResponse"/>
    /// rút gọn và chỉ với ca của chính họ đã được duyệt (GB-05). Swagger chỉ hiển thị được
    /// một hình dạng nên phần mô tả này là nơi ghi lại điều đó.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "DOCTOR,NURSE,PATIENT")]
    [ProducesResponseType(typeof(ApiResponse<CaseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (User.IsInRole("PATIENT"))
        {
            var patientView = await _cases.GetForPatientAsync(id, GetCallerUserId(), ct);
            return Ok(ApiResponse<PatientCaseResponse>.Ok(patientView));
        }

        var staffView = await _cases.GetForStaffAsync(id, ct);
        return Ok(ApiResponse<CaseResponse>.Ok(staffView));
    }

    /// <summary>
    /// Nurse checkin appointment thông qua case.
    /// Appointment: Booked → Approved
    /// </summary>
    [HttpPost("{caseId:guid}/appointment/checkin")]
    [Authorize(Roles = "NURSE,ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckinAppointment(
        Guid caseId,
        CancellationToken ct)
    {
        var result = await _appointmentService.CheckinAppointmentAsync(caseId, ct);
        return Ok(ApiResponse<AppointmentResponse>.Ok(result, "Appointment checked in successfully."));
    }

    /// <summary>
    /// Tạo lần khám mới (UC-07), request multipart. Ảnh siêu âm tùy chọn (quyết định ghi đè
    /// 07/08/2026) — bỏ trống được, bổ sung sau qua AddImages (`#21`, vẫn bắt buộc ≥1 ảnh).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "DOCTOR,NURSE")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(120L * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<CaseResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromForm] Guid patientProfileId,
        [FromForm] Guid responsibleDoctorId,
        [FromForm] string? clinicalInfo,
        [FromForm] string? symptomsJson,
        [FromForm] List<IFormFile> images,
        CancellationToken ct)
    {
        IReadOnlyList<CreateCaseSymptomRequest>? symptoms = null;
        if (!string.IsNullOrWhiteSpace(symptomsJson))
        {
            try
            {
                symptoms = System.Text.Json.JsonSerializer.Deserialize<List<CreateCaseSymptomRequest>>(
                    symptomsJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return BadRequest(ApiResponse<object>.Fail(StatusCodes.Status400BadRequest, "Invalid symptoms JSON format."));
            }
        }

        var request = new CreateCaseRequest(
            patientProfileId, responsibleDoctorId, clinicalInfo, symptoms, ToUploadedFiles(images));

        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<object>.Fail(StatusCodes.Status400BadRequest, message));
        }

        var result = await _cases.CreateAsync(request, ct);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.CaseId },
            ApiResponse<CaseResponse>.Ok(result, "Case created successfully"));
    }

    /// <summary>
    /// Thêm 07/08/2026 — "Lưu kết luận". Bác sĩ phụ trách nhập/sửa kết luận nhiều lần, KHÔNG
    /// đổi trạng thái ca. CHỈ Bác sĩ, và CHỈ đúng bác sĩ phụ trách của ca này (GB-04). Ca đã
    /// CONFIRMED thì từ chối luôn (GB-01/P2 — không sửa hồ sơ đã khoá).
    /// </summary>
    [HttpPut("{caseId:guid}/conclusion")]
    [Authorize(Roles = "DOCTOR")]
    [ProducesResponseType(typeof(ApiResponse<CaseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SaveConclusion(
        Guid caseId,
        [FromBody] CaseConclusionRequest request,
        CancellationToken ct)
    {
        var validation = await _conclusionValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<object>.Fail(StatusCodes.Status400BadRequest, message));
        }

        var result = await _cases.SaveConclusionAsync(caseId, GetCallerUserId(), request, ct);

        return Ok(ApiResponse<CaseResponse>.Ok(result, "Case conclusion saved successfully"));
    }

    /// <summary>
    /// Thêm 07/08/2026 — "Kết thúc ca khám". Lưu VÀ khoá ca (CONFIRMED) trong cùng một lần
    /// gọi, thay vì đợi màn duyệt kết quả AI riêng (UC-19, đang được một luồng công việc khác
    /// xây song song). CHỈ Bác sĩ, và CHỈ đúng bác sĩ phụ trách của ca này (GB-04). Lưu thành
    /// công là ca chuyển CONFIRMED — trạng thái cuối, không có đường lùi (GB-01/P2).
    /// </summary>
    [HttpPut("{caseId:guid}/confirm")]
    [Authorize(Roles = "DOCTOR")]
    [ProducesResponseType(typeof(ApiResponse<CaseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Confirm(
        Guid caseId,
        [FromBody] CaseConclusionRequest request,
        CancellationToken ct)
    {
        var validation = await _conclusionValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<object>.Fail(StatusCodes.Status400BadRequest, message));
        }

        var result = await _cases.ConfirmAsync(caseId, GetCallerUserId(), request, ct);

        return Ok(ApiResponse<CaseResponse>.Ok(result, "Case confirmed successfully"));
    }

    /// <summary>
    /// Chuyển thẳng ca từ CONFIRMED sang END đối với những ca không kê đơn thuốc.
    /// </summary>
    [HttpPut("{caseId:guid}/end")]
    [Authorize(Roles = "DOCTOR")]
    [ProducesResponseType(typeof(ApiResponse<CaseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> End(
        Guid caseId,
        CancellationToken ct)
    {
        var result = await _cases.EndWithoutPrescriptionAsync(caseId, GetCallerUserId(), ct);

        return Ok(ApiResponse<CaseResponse>.Ok(result, "Case ended successfully without prescription"));
    }

    /// <summary>
    /// Xuất báo cáo PDF của một lần khám đã duyệt (UC-12).
    ///
    /// Đây là endpoint DUY NHẤT không bọc trong khuôn {code, message, data} — thân phản hồi
    /// là byte của file PDF. Riêng nhánh lỗi thì vẫn dùng khuôn JSON như mọi chỗ khác, vì
    /// lúc đó chưa có file nào để trả.
    ///
    /// Chỉ Bác sĩ/Điều dưỡng. Bệnh nhân xem được cùng nội dung trên Mobile (UC-08) nhưng
    /// không có chức năng xuất file (quyết định UCS ngày 01/08/2026).
    /// </summary>
    [HttpGet("{id:guid}/report")]
    [Authorize(Roles = "DOCTOR,NURSE")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ExportReport(Guid id, CancellationToken ct)
    {
        var pdf = await _reportsLazy.Value.GenerateReportAsync(id, ct);
        return File(pdf, "application/pdf", $"visit-report-{id}.pdf");
    }

    /// <summary>
    /// Lấy đơn thuốc mới nhất của một ca (Module 7 — case detail hiển thị đơn sau khi kê).
    /// </summary>
    [HttpGet("{caseId:guid}/prescription")]
    [Authorize(Roles = "DOCTOR,NURSE")]
    [ProducesResponseType(typeof(ApiResponse<PrescriptionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCasePrescription(Guid caseId, CancellationToken ct)
    {
        var prescription = await _prescriptions.GetByCaseIdAsync(caseId, ct);
        if (prescription is null)
            return Ok(ApiResponse<object>.Ok(null, "No prescription for this case."));
        return Ok(ApiResponse<PrescriptionResponse>.Ok(prescription));
    }

    /// <summary>
    /// UC-18 + compliance history — lấy toàn bộ đơn của một ca kèm % tuân thủ.
    /// Chỉ đơn do bác sĩ hiện tại kê mới có AdherencePercent;
    /// đơn bác sĩ khác → null (GB guard).
    /// </summary>
    [HttpGet("{caseId:guid}/prescriptions/with-compliance")]
    [Authorize(Roles = "DOCTOR,NURSE")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PrescriptionWithComplianceResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCasePrescriptionsWithCompliance(Guid caseId, CancellationToken ct)
    {
        var result = await _prescriptions.GetCasePrescriptionsWithComplianceAsync(
            GetCallerUserId(), caseId, ct);
        return Ok(ApiResponse<IReadOnlyList<PrescriptionWithComplianceResponse>>.Ok(result));
    }

    /// <summary>
    /// Quy đổi IFormFile (kiểu của tầng web) sang UploadedFile (kiểu trung tính của BLL).
    /// </summary>
    private static List<UploadedFile> ToUploadedFiles(List<IFormFile> files) =>
        files
            .Select(f => new UploadedFile(
                FileName: f.FileName,
                ContentType: f.ContentType,
                Length: f.Length,
                Content: f.OpenReadStream()))
            .ToList();

    private Guid GetCallerUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new UnauthorizedAccessException("Invalid access token.");
}
