using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Exceptions;
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
    private readonly IFileStorageService _storage;
    private readonly ILogger<CaseService> _logger;

    public CaseService(
        ICaseRepository cases,
        IUltrasoundImageRepository images,
        IPatientProfileRepository profiles,
        IUserRepository users,
        IFileStorageService storage,
        ILogger<CaseService> logger)
    {
        _cases = cases;
        _images = images;
        _profiles = profiles;
        _users = users;
        _storage = storage;
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
        if (medicalCase is null
            || medicalCase.PatientProfileId != profile.PatientProfileId
            || medicalCase.Status != CaseStatus.Confirmed)
        {
            throw new ResourceNotFoundException("Case not found.");
        }

        return CaseMapper.ToPatientResponse(medicalCase);
    }

    public async Task<PagedResult<CaseSummaryResponse>> ListByPatientProfileAsync(
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
                throw new BusinessException("Status must be CREATED, ANALYZED or CONFIRMED.");
            }

            statusFilter = parsed;
        }

        var (items, total) = await _cases.SearchByPatientAsync(
            patientProfileId, statusFilter, sortOrder, page, pageSize, ct);

        return ToPagedResult(items, page, pageSize, total);
    }

    public async Task<PagedResult<CaseSummaryResponse>> ListMineAsync(
        Guid callerUserId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var profile = await _profiles.GetByUserIdAsync(callerUserId, ct)
            ?? throw new ResourceNotFoundException("You do not have a patient profile yet.");

        // CONFIRMED ép cứng ở đây, KHÔNG nhận từ client. Để client chọn trạng thái là mở
        // đường cho bệnh nhân đọc kết quả AI chưa được bác sĩ duyệt (GB-05).
        var (items, total) = await _cases.SearchByPatientAsync(
            profile.PatientProfileId, CaseStatus.Confirmed, "desc", page, pageSize, ct);

        return ToPagedResult(items, page, pageSize, total);
    }

    public async Task<CaseResponse> CreateAsync(
        CreateCaseRequest request,
        CancellationToken ct = default)
    {
        // AF-02 / BR-02: chặn ở đây chứ không ở FluentValidation, vì đặc tả quy định lỗi này
        // trả 422 còn validator thì luôn cho ra 400.
        if (request.Images.Count == 0)
        {
            throw new BusinessException("A Case must have at least 1 ultrasound image.");
        }

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
            await _storage.DeleteAsync(path, ct);
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

    private static PagedResult<CaseSummaryResponse> ToPagedResult(
        IReadOnlyList<Case> items,
        int page,
        int pageSize,
        int total)
    {
        var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)pageSize);

        return new PagedResult<CaseSummaryResponse>(
            items.Select(CaseMapper.ToSummary).ToList(),
            page,
            pageSize,
            total,
            totalPages);
    }
}
