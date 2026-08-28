using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.BLL.MedicalRecord.Mappers;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.ExternalServices;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.MedicalRecord.Services;

/// <summary>
/// UC-07 và UC-08 — ca khám, ảnh siêu âm, và quyền đọc theo vai trò.
/// </summary>
public sealed class CaseService : ICaseService
{
    private readonly ICaseRepository _cases;
    private readonly IUltrasoundImageRepository _images;
    private readonly IPatientProfileRepository _profiles;
    private readonly IUserRepository _users;
    private readonly System.Lazy<IFileStorageService> _storageLazy;
    private readonly INotificationService _notificationService;
    private readonly IAppointmentRepository _appointments;
    private readonly ILogger<CaseService> _logger;

    private IFileStorageService _storage => _storageLazy.Value;

    public CaseService(
        ICaseRepository cases,
        IUltrasoundImageRepository images,
        IPatientProfileRepository profiles,
        IUserRepository users,
        System.Lazy<IFileStorageService> storageLazy,
        INotificationService notificationService,
        IAppointmentRepository appointments,
        ILogger<CaseService> logger)
    {
        _cases = cases;
        _images = images;
        _profiles = profiles;
        _users = users;
        _storageLazy = storageLazy;
        _notificationService = notificationService;
        _appointments = appointments;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UltrasoundImageResponse>> ListImagesAsync(
        Guid caseId,
        CancellationToken ct = default)
    {
        _ = await _cases.GetByIdAsync(caseId, ct)
            ?? throw new ResourceNotFoundException("Case not found.");

        var images = await _images.ListByCaseAsync(caseId, ct);
        var urls = await BuildImageUrlsAsync(images, ct);

        return images
            .Select(i => CaseMapper.ToImageResponse(i, urls.GetValueOrDefault(i.ImageId)))
            .ToList();
    }

    public async Task<CaseResponse> GetForStaffAsync(Guid caseId, CancellationToken ct = default)
    {
        var medicalCase = await _cases.GetDetailAsync(caseId, ct)
            ?? throw new ResourceNotFoundException("Case not found.");

        var urls = await BuildImageUrlsAsync(medicalCase.UltrasoundImages.ToList(), ct);

        return CaseMapper.ToStaffResponse(medicalCase, urls);
    }

    public async Task<PatientCaseResponse> GetForPatientAsync(
        Guid caseId,
        Guid callerUserId,
        CancellationToken ct = default)
    {
        var profile = await _profiles.GetByUserIdAsync(callerUserId, ct)
            ?? throw new ResourceNotFoundException("Case not found.");

        var medicalCase = await _cases.GetDetailAsync(caseId, ct);

        // Ba điều kiện trượt đều trả về CÙNG một lỗi 404. Trả 403 cho ca chưa duyệt là gián
        // tiếp xác nhận "có tồn tại một ca như vậy" — đúng thứ GB-05 không cho lộ.
        // Quyết định 14/08/2026 (sau khi trao đổi lại): Patient CHỈ xem được ca đã END (đã
        // Confirmed VÀ đã có đơn thuốc) — ca mới Confirmed nhưng chưa kê đơn vẫn ẩn, kể cả
        // khi Patient có ID trực tiếp.
        if (medicalCase is null
            || medicalCase.PatientProfileId != profile.PatientProfileId
            || medicalCase.Status != CaseStatus.End)
        {
            throw new ResourceNotFoundException("Case not found.");
        }

        var urls = await BuildImageUrlsAsync(medicalCase.UltrasoundImages.ToList(), ct);
        return CaseMapper.ToPatientResponse(medicalCase, urls);
    }

    public async Task<PagedResult<StaffCaseSummaryResponse>> ListByPatientProfileAsync(
        Guid patientProfileId,
        string? status,
        string sortOrder,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        _ = await _profiles.GetByIdAsync(patientProfileId, ct)
            ?? throw new ResourceNotFoundException("Patient profile not found.");

        CaseStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<CaseStatus>(status, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            {
                throw new BusinessException("Status must be CREATED, END or CONFIRMED.");
            }

            statusFilter = parsed;
        }

        var (items, total) = await _cases.SearchByPatientAsync(
            patientProfileId,
            statusFilter.HasValue ? new[] { statusFilter.Value } : null,
            sortOrder, page, pageSize, ct);

        return ToPagedResult(items, page, pageSize, total, CaseMapper.ToStaffSummary);
    }

    public async Task<PagedResult<CaseSummaryResponse>> ListMineAsync(
        Guid callerUserId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var profile = await _profiles.GetByUserIdAsync(callerUserId, ct)
            ?? throw new ResourceNotFoundException("You do not have a patient profile yet.");

        // END ép cứng ở đây, KHÔNG nhận từ client. Để client chọn trạng thái là mở đường cho
        // bệnh nhân đọc kết quả AI chưa được bác sĩ duyệt (GB-05).
        // Quyết định 14/08/2026 (sau khi trao đổi lại): Patient CHỈ xem được ca đã END (đã
        // Confirmed VÀ đã có đơn thuốc) — ca mới Confirmed nhưng bác sĩ chưa kê đơn thì KHÔNG
        // hiện trong danh sách của chính patient (đảo ngược quyết định Confirmed+End cùng ngày
        // trước đó — team đã trao đổi lại và chốt: chỉ End mới coi là "đã hoàn tất lượt khám").
        var (items, total) = await _cases.SearchByPatientAsync(
            profile.PatientProfileId,
            new[] { CaseStatus.End },
            "desc", page, pageSize, ct);

        return ToPagedResult(items, page, pageSize, total, CaseMapper.ToSummary);
    }

    public async Task<CaseResponse> CreateAsync(
        CreateCaseRequest request,
        CancellationToken ct = default)
    {
        // Quyết định ghi đè 07/08/2026 — BR-02 gốc ("phải có ít nhất 1 ảnh siêu âm") không còn
        // áp dụng cho việc TẠO ca khám nữa: không phải lần khám nào cũng chụp siêu âm ngay lúc
        // tiếp nhận. Ảnh giờ hoàn toàn tùy chọn ở #20, bổ sung sau qua #21 (AddUltrasoundImagesAsync
        // — luật đó KHÔNG đổi, #21 vẫn bắt buộc ≥1 ảnh vì "bổ sung 0 ảnh" là một no-op vô nghĩa).
        var profile = await _profiles.GetByIdAsync(request.PatientProfileId, ct)
            ?? throw new ResourceNotFoundException("Patient profile not found.");

        var doctor = await _users.GetByIdAsync(request.ResponsibleDoctorId, ct)
            ?? throw new ResourceNotFoundException("Responsible doctor not found.");

        // GB-04: mỗi ca phải quy về đúng một bác sĩ chịu trách nhiệm. Điều dưỡng tạo hộ thì
        // vẫn phải chọn bác sĩ — không bao giờ tự suy ra từ người đang đăng nhập.
        if (doctor.Role != UserRole.Doctor)
        {
            throw new BusinessException("The responsible doctor must be a doctor account.");
        }

        var caseId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var (images, uploadedPaths) = await UploadImagesAsync(caseId, request.Images, note: null, ct);

        var newCase = new Case
        {
            CaseId = caseId,
            PatientProfileId = profile.PatientProfileId,
            DoctorId = doctor.UserId,
            VisitDate = ClinicClock.Today(),
            ClinicalInfo = request.ClinicalInfo,
            Status = CaseStatus.Created,
            CreatedAt = now,
            UpdatedAt = now,
            CaseSymptoms = request.Symptoms?.Select(s => new CaseSymptom
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                CategoryId = s.CategoryId,
                SymptomId = s.SymptomId,
                OtherNote = s.OtherNote,
                CreatedAt = now
            }).ToList() ?? new List<CaseSymptom>()
        };

        try
        {
            await _cases.CreateWithImagesAsync(newCase, images, ct);
        }
        catch
        {
            // File đã nằm trên Storage rồi mà bản ghi thì không ghi được — dọn file đi.
            // Làm ngược lại (ghi DB trước) sẽ để lại bản ghi trỏ vào file không tồn tại, mà
            // GB-03 cấm xoá bản ghi y tế nên hỏng là hỏng vĩnh viễn.
            await CleanUpAsync(uploadedPaths, ct);
            throw;
        }

        _logger.LogInformation(
            "Case {CaseId} created for patient profile {PatientProfileId} with {ImageCount} image(s)",
            caseId, profile.PatientProfileId, images.Count);

        // Send notification to patient about new medical record (best effort - don't fail case creation)
        try
        {
            var patientUserId = profile.UserId;
            await _notificationService.SendAsync(new SendNotificationRequest
            {
                UserId = patientUserId,
                Type = "medical_record_added",
                Title = "Hồ sơ y tế mới được tạo",
                Body = $"BS. {doctor.FullName} đã tạo hồ sơ khám cho bạn vào ngày {ClinicClock.Today():dd/MM/yyyy}.",
                Metadata = new Dictionary<string, object>
                {
                    ["caseId"] = caseId.ToString(),
                    ["recordId"] = caseId.ToString()
                }
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send medical record notification for case {CaseId}", caseId);
        }

        return await GetForStaffAsync(caseId, ct);
    }

    public async Task<IReadOnlyList<UltrasoundImageResponse>> AddImagesAsync(
        Guid caseId,
        AddUltrasoundImagesRequest request,
        CancellationToken ct = default)
    {
        var medicalCase = await _cases.GetByIdAsync(caseId, ct)
            ?? throw new ResourceNotFoundException("Case not found.");

        // GB-01: ca đã chốt thì không mở lại để nhận thêm đầu vào.
        if (medicalCase.Status == CaseStatus.Confirmed)
        {
            throw new BusinessException("This case is already confirmed and cannot accept more images.");
        }

        var (images, uploadedPaths) = await UploadImagesAsync(caseId, request.Images, request.Note, ct);

        try
        {
            await _images.AddRangeAsync(images, ct);
        }
        catch
        {
            await CleanUpAsync(uploadedPaths, ct);
            throw;
        }

        _logger.LogInformation("Added {ImageCount} image(s) to case {CaseId}", images.Count, caseId);

        var urls = await BuildImageUrlsAsync(images, ct);

        return images
            .Select(i => CaseMapper.ToImageResponse(i, urls.GetValueOrDefault(i.ImageId)))
            .ToList();
    }

    public async Task<CaseResponse> SaveConclusionAsync(
        Guid caseId,
        Guid actingDoctorId,
        CaseConclusionRequest request,
        CancellationToken ct = default)
    {
        var medicalCase = await LoadForConclusionUpdateAsync(caseId, actingDoctorId, ct);

        medicalCase.FinalDiagnosis = request.FinalDiagnosis.Trim();
        medicalCase.DoctorConclusion = request.DoctorConclusion.Trim();
        medicalCase.UpdatedAt = DateTime.UtcNow;
        // Trạng thái CỐ Ý không đổi — đây là lưu nháp, sửa lại được nhiều lần cho tới khi
        // Bác sĩ bấm "Kết thúc ca khám" (ConfirmAsync).

        await _cases.SaveChangesAsync(ct);

        _logger.LogInformation("Case {CaseId} conclusion saved by doctor {DoctorId}", caseId, actingDoctorId);

        return await GetForStaffAsync(caseId, ct);
    }

    public async Task<CaseResponse> ConfirmAsync(
        Guid caseId,
        Guid actingDoctorId,
        CaseConclusionRequest request,
        CancellationToken ct = default)
    {
        var medicalCase = await LoadForConclusionUpdateAsync(caseId, actingDoctorId, ct);

        medicalCase.FinalDiagnosis = request.FinalDiagnosis.Trim();
        medicalCase.DoctorConclusion = request.DoctorConclusion.Trim();
        medicalCase.Status = CaseStatus.Confirmed;
        medicalCase.UpdatedAt = DateTime.UtcNow;

        await _cases.SaveChangesAsync(ct);

        _logger.LogInformation("Case {CaseId} confirmed by doctor {DoctorId}", caseId, actingDoctorId);

        return await GetForStaffAsync(caseId, ct);
    }

    public async Task<CaseResponse> EndWithoutPrescriptionAsync(
        Guid caseId,
        Guid actingDoctorId,
        CancellationToken ct = default)
    {
        var medicalCase = await _cases.GetForUpdateAsync(caseId, ct)
            ?? throw new ResourceNotFoundException("Case not found.");

        if (medicalCase.DoctorId != actingDoctorId)
        {
            throw new BusinessException("Only the responsible doctor can end this case.");
        }

        if (medicalCase.Status != CaseStatus.Confirmed)
        {
            throw new BusinessException("Only confirmed cases can be ended without prescription.");
        }

        medicalCase.Status = CaseStatus.End;
        medicalCase.UpdatedAt = DateTime.UtcNow;

        // Complete related appointment if exists and is Approved
        await CompleteRelatedAppointmentAsync(caseId, ct);

        await _cases.SaveChangesAsync(ct);

        _logger.LogInformation("Case {CaseId} ended without prescription by doctor {DoctorId}", caseId, actingDoctorId);

        return await GetForStaffAsync(caseId, ct);
    }

    /// <summary>
    /// Complete appointment when case is ended.
    /// </summary>
    private async Task CompleteRelatedAppointmentAsync(Guid caseId, CancellationToken ct)
    {
        // Get all appointments for the patient and find the one linked to this case
        // Note: This is a simplified approach. In production, you might want to add
        // a specific method to IAppointmentRepository to query by CaseId.
        var appointments = await _appointments.ListByPatientAsync(
            (await _cases.GetByIdAsync(caseId, ct))!.PatientProfileId, ct);

        var appointment = appointments
            .FirstOrDefault(a => a.CaseId == caseId && a.Status == AppointmentStatus.Approved);

        if (appointment != null)
        {
            appointment.Status = AppointmentStatus.Completed;
            appointment.UpdatedAt = DateTime.UtcNow;
            await _appointments.UpdateAsync(appointment, ct);

            _logger.LogInformation(
                "Appointment {AppointmentId} completed when case {CaseId} was ended",
                appointment.AppointmentId, caseId);
        }
    }

    /// <inheritdoc />
    public async Task<Guid> CreateFromBookingAsync(
        Guid patientProfileId,
        Guid doctorId,
        DateOnly visitDate,
        IReadOnlyList<SymptomInput> symptoms,
        CancellationToken ct = default)
    {
        var caseId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var newCase = new Case
        {
            CaseId = caseId,
            PatientProfileId = patientProfileId,
            DoctorId = doctorId,
            VisitDate = visitDate,
            ClinicalInfo = null, // Sẽ được bác sĩ cập nhật khi khám
            Status = CaseStatus.Booked,
            CreatedAt = now,
            UpdatedAt = now,
            CaseSymptoms = symptoms.Select(s => new CaseSymptom
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                CategoryId = s.CategoryId,
                SymptomId = s.SymptomId,
                OtherNote = s.OtherNote,
                CreatedAt = now
            }).ToList()
        };

        await _cases.CreateAsync(newCase, ct);

        _logger.LogInformation(
            "Case {CaseId} created from appointment booking for patient profile {PatientProfileId} with {SymptomCount} symptoms",
            caseId, patientProfileId, symptoms.Count);

        return caseId;
    }

    /// <summary>
    /// Tải ca (có theo dõi) và kiểm hai điều kiện dùng chung cho cả SaveConclusionAsync lẫn
    /// ConfirmAsync — tách ra một chỗ để hai hành động không bao giờ lệch luật với nhau.
    /// </summary>
    private async Task<Case> LoadForConclusionUpdateAsync(
        Guid caseId, Guid actingDoctorId, CancellationToken ct)
    {
        var medicalCase = await _cases.GetForUpdateAsync(caseId, ct)
            ?? throw new ResourceNotFoundException("Case not found.");

        // P2/GB-01 — CONFIRMED là trạng thái cuối, không có đường lùi. Ca đã khoá thì không
        // sửa được nữa dưới bất kỳ hình thức nào, kể cả chỉ lưu nháp lại đúng nội dung cũ.
        if (medicalCase.Status == CaseStatus.Confirmed)
        {
            throw new BusinessException("This case has already been confirmed and cannot be changed.");
        }

        // GB-04 — chỉ đúng bác sĩ chịu trách nhiệm của CA NÀY mới được sửa/chốt kết luận,
        // không phải bác sĩ bất kỳ đang đăng nhập.
        if (medicalCase.DoctorId != actingDoctorId)
        {
            throw new BusinessException("Only the responsible doctor can change this case's conclusion.");
        }

        return medicalCase;
    }

    /// <summary>
    /// Kiểm rồi đẩy từng file lên Storage. Hỏng giữa chừng thì dọn sạch những file đã lên
    /// trước khi ném tiếp — không để lại rác nửa vời.
    /// </summary>
    private async Task<(List<UltrasoundImage> Images, List<string> UploadedPaths)> UploadImagesAsync(
        Guid caseId,
        IReadOnlyList<UploadedFile> files,
        string? note,
        CancellationToken ct)
    {
        var images = new List<UltrasoundImage>(files.Count);
        var uploadedPaths = new List<string>(files.Count);
        var now = DateTime.UtcNow;

        try
        {
            foreach (var file in files)
            {
                var contentType = await UltrasoundImageContentValidator
                    .ValidateAndResolveContentTypeAsync(file, ct);

                var imageId = Guid.NewGuid();
                var extension = contentType == "image/png" ? ".png" : ".jpg";
                var objectPath = $"{caseId}/{imageId}{extension}";

                await _storage.UploadAsync(file.Content, objectPath, contentType, ct);
                uploadedPaths.Add(objectPath);

                images.Add(new UltrasoundImage
                {
                    ImageId = imageId,
                    CaseId = caseId,
                    FileRef = objectPath,
                    UploadedAt = now,
                    Note = note,
                });
            }
        }
        catch
        {
            await CleanUpAsync(uploadedPaths, ct);
            throw;
        }

        return (images, uploadedPaths);
    }

    private async Task CleanUpAsync(IReadOnlyList<string> objectPaths, CancellationToken ct)
    {
        foreach (var path in objectPaths)
        {
            try
            {
                await _storage.DeleteAsync(path, ct);
            }
            catch (Exception exception)
            {
                // Best-effort cleanup on an already-failing rollback path. One failed delete
                // must not mask the original exception that triggered this cleanup, nor stop
                // us from attempting to clean up the rest of the batch.
                _logger.LogWarning(exception, "Failed to delete orphaned object {ObjectPath} during rollback", path);
            }
        }
    }

    /// <summary>
    /// Ký URL cho từng ảnh. Ảnh nào ký hỏng thì nhận giá trị null và hồ sơ vẫn hiển thị —
    /// dữ liệu seed có file_ref trỏ vào object không tồn tại, ném ở đây là hỏng cả màn hình.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, string?>> BuildImageUrlsAsync(
        IReadOnlyList<UltrasoundImage> images,
        CancellationToken ct)
    {
        var urls = new Dictionary<Guid, string?>(images.Count);

        foreach (var image in images)
        {
            urls[image.ImageId] = await _storage.CreateSignedUrlAsync(image.FileRef, ct);
        }

        return urls;
    }

    /// <summary>
    /// Dùng chung cho cả #24 (StaffCaseSummaryResponse) và #25 (CaseSummaryResponse) — hai
    /// endpoint map cùng một trang Case entity sang hai response khác nhau (#24 thêm
    /// CreatedAt), nên phần phân trang tách hẳn khỏi phần map từng dòng qua tham số map.
    /// </summary>
    private static PagedResult<T> ToPagedResult<T>(
        IReadOnlyList<Case> items,
        int page,
        int pageSize,
        int total,
        Func<Case, T> map)
    {
        var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)pageSize);

        return new PagedResult<T>(
            items.Select(map).ToList(),
            page,
            pageSize,
            total,
            totalPages);
    }
}
