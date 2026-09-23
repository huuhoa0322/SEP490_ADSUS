using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.AIDiagnosis.Services;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.ExternalServices;
using ADSUS_BE.DAL.Repositories.Interfaces;
using ADSUS_BE.BLL.CaseClinicServices;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Exceptions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.MedicalRecord.Services;

/// <summary>
/// AppDbContext vẫn được inject, nhưng CHỈ để mở transaction bao ngoài (Database
/// .BeginTransactionAsync/CommitAsync/RollbackAsync) — mọi thao tác đọc/ghi entity giờ đi
/// qua Repository (P11 review Feature 4, 29/08/2026); trước đây gọi thẳng
/// _db.UltrasoundImages/.AiPredictions/.DoctorAnnotations/.AiModelVersions. Nhiều
/// SaveChangesAsync() gọi tuần tự bên trong 1 transaction vẫn atomic — chưa gì commit thật
/// cho tới transaction.CommitAsync() cuối cùng, rollback sẽ hoàn tác tất cả.
/// </summary>
public sealed class CaseDiagnosisService : ICaseDiagnosisService
{
    private readonly AppDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAiModelVersionRepository _aiModelVersionRepo;
    private readonly IUltrasoundImageRepository _images;
    private readonly IAiPredictionRepository _predictions;
    private readonly IDoctorAnnotationRepository _annotations;
    private readonly ICaseRepository _cases;
    private readonly ILogger<CaseDiagnosisService> _logger;
    private readonly ICaseClinicServiceService? _caseClinicServiceService;
    private readonly string _aiBackendUrl;
    private readonly string? _aiBackendToken;

    private readonly IAiDiagnosisStateTracker _tracker;

    public CaseDiagnosisService(
        AppDbContext db,
        IFileStorageService storage,
        IHttpClientFactory httpClientFactory,
        IAiModelVersionRepository aiModelVersionRepo,
        IUltrasoundImageRepository images,
        IAiPredictionRepository predictions,
        IDoctorAnnotationRepository annotations,
        ICaseRepository cases,
        IConfiguration configuration,
        ILogger<CaseDiagnosisService> logger,
        IAiDiagnosisStateTracker tracker,
        ICaseClinicServiceService? caseClinicServiceService = null)
    {
        _db = db;
        _storage = storage;
        _httpClientFactory = httpClientFactory;
        _aiModelVersionRepo = aiModelVersionRepo;
        _images = images;
        _predictions = predictions;
        _annotations = annotations;
        _cases = cases;
        _logger = logger;
        _tracker = tracker;
        _caseClinicServiceService = caseClinicServiceService;

        var configuredUrl = configuration["AiBackend:WebhookUrl"];
        if (string.IsNullOrEmpty(configuredUrl))
        {
            // P11 review (Feature 4, 29/08/2026): trước đây fallback âm thầm về localhost —
            // nếu thiếu config ở production thì mọi request AI sẽ lỗi kết nối khó hiểu thay vì
            // báo rõ nguyên nhân. Vẫn giữ fallback (không đổi hành vi dev hiện tại) nhưng cảnh
            // báo rõ ràng qua log để không bị bỏ sót.
            _logger.LogWarning(
                "AiBackend:WebhookUrl is not configured — falling back to http://localhost:8000. " +
                "This is only correct for local development; set it explicitly in production.");
        }
        _aiBackendUrl = configuredUrl ?? "http://localhost:8000";
        _aiBackendToken = configuration["AiBackend:Token"];
    }

    /// <summary>
    /// Bác sĩ chỉ thao tác được với ca sau khi Điều dưỡng check-in (Booked → InProgress) —
    /// cùng luật với CaseService.GetForStaffAsync/LoadForConclusionUpdateAsync (yêu cầu
    /// 10/09/2026). Trước đây service này không load Case entity ở đâu cả (chỉ ghi
    /// UltrasoundImage/AiPrediction/DoctorAnnotation qua caseId thô), nên đây cũng là lần đầu
    /// tiên có một bước kiểm tra tồn tại + trạng thái của ca trước khi xử lý ảnh AI.
    /// </summary>
    private async Task EnsureCaseCheckedInAsync(Guid caseId, CancellationToken ct)
    {
        var medicalCase = await _cases.GetByIdAsync(caseId, ct)
            ?? throw new ResourceNotFoundException("Case not found.");

        if (medicalCase.Status == CaseStatus.Booked)
        {
            throw new BusinessException(
                "This case has not been checked in yet. Please wait for the nurse to check in the patient first.");
        }
    }

    public async Task<JsonElement> AnalyzeImageAsync(Guid caseId, Stream imageStream, string fileName, string contentType, CancellationToken ct = default)
    {
        await EnsureCaseCheckedInAsync(caseId, ct);

        if (imageStream.CanSeek && imageStream.Length > UltrasoundImageContentValidator.MaxFileSizeBytes)
        {
            throw new BusinessException("Kích thước ảnh vượt quá giới hạn 20MB.");
        }

        // Tạo UploadedFile wrapper để sử dụng Validator
        var uploadedFile = new UploadedFile(fileName, contentType, imageStream.CanSeek ? imageStream.Length : 0, imageStream);

        // Validate file signature (magic bytes) trước khi gửi cho AI
        var resolvedContentType = await UltrasoundImageContentValidator
            .ValidateAndResolveContentTypeAsync(uploadedFile);
        if (imageStream.CanSeek) 
        {
            imageStream.Seek(0, SeekOrigin.Begin); // Reset stream position sau khi đọc magic bytes
        }

        // Ignore the modelVersionId passed from frontend and fetch the true ACTIVE model
        var activeModel = await _aiModelVersionRepo.GetActiveVersionReadOnlyAsync(ct);
        if (activeModel == null) throw new BusinessException("Hệ thống chưa có phiên bản AI nào được kích hoạt. Vui lòng liên hệ Admin.");

        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(2); // AI might take time

        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(imageStream);
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        content.Add(streamContent, "file", fileName);
        
        content.Add(new StringContent(activeModel.HfRepoId), "repo_id");
        content.Add(new StringContent(activeModel.HfFilename), "filename");

        // AI Backend co the co URL public (Render) nen /api/detect doi hoi Bearer token,
        // giong het cach AiModelService da lam voi /api/reload-model.
        if (!string.IsNullOrEmpty(_aiBackendToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _aiBackendToken);
        }

        // Send to Python backend which uses its currently loaded model
        HttpResponseMessage response;

        _tracker.BeginDiagnosis();
        try
        {
            response = await client.PostAsync($"{_aiBackendUrl}/api/detect", content, ct);
        }
        catch (HttpRequestException)
        {
            throw new BusinessException("Hệ thống AI Backend đang tắt hoặc không thể kết nối. Vui lòng bật AI Backend (hoặc cấu hình Ngrok) trước khi tải ảnh.");
        }
        finally
        {
            _tracker.EndDiagnosis();
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new BusinessException($"Lỗi từ hệ thống AI: {error}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    public async Task ConfirmAnalysisAsync(Guid caseId, ConfirmAnalysisRequest request, CancellationToken ct = default)
    {
        await EnsureCaseCheckedInAsync(caseId, ct);

        if (request.OriginalImageStream.CanSeek && request.OriginalImageStream.Length > UltrasoundImageContentValidator.MaxFileSizeBytes)
        {
            throw new BusinessException("Kích thước ảnh gốc vượt quá giới hạn 20MB.");
        }

        if (request.BurntImageStream.CanSeek && request.BurntImageStream.Length > UltrasoundImageContentValidator.MaxFileSizeBytes)
        {
            throw new BusinessException("Kích thước ảnh kết quả vượt quá giới hạn 20MB.");
        }

        // NFR-SEC-07: xác thực nội dung thật (magic bytes) chứ không chỉ tin filename/Content-Type
        // client tự khai — AnalyzeImageAsync đã làm việc này, ConfirmAnalysisAsync trước đây bỏ sót.
        var originalUploadedFile = new UploadedFile(
            request.OriginalImageFileName, request.OriginalImageContentType,
            request.OriginalImageStream.CanSeek ? request.OriginalImageStream.Length : 0, request.OriginalImageStream);
        await UltrasoundImageContentValidator.ValidateAndResolveContentTypeAsync(originalUploadedFile, ct);

        var burntUploadedFile = new UploadedFile(
            request.BurntImageFileName, request.BurntImageContentType,
            request.BurntImageStream.CanSeek ? request.BurntImageStream.Length : 0, request.BurntImageStream);
        await UltrasoundImageContentValidator.ValidateAndResolveContentTypeAsync(burntUploadedFile, ct);

        // Fetch true active ModelVersionId for database tracking (using GetActiveVersionAsync for entity tracking)
        var activeModel = await _aiModelVersionRepo.GetActiveVersionAsync(ct)
            ?? await _aiModelVersionRepo.GetActiveVersionReadOnlyAsync(ct);
        if (activeModel == null) throw new BusinessException("Hệ thống chưa có phiên bản AI nào được kích hoạt. Vui lòng liên hệ Admin.");
        var activeModelId = activeModel.ModelVersionId;

        // Check if an existing image in the case is being re-diagnosed / having calipers updated
        var caseImages = await _images.ListByCaseAsync(caseId, ct);
        UltrasoundImage? existingImage = null;
        if (caseImages.Count > 0)
        {
            existingImage = caseImages.FirstOrDefault(img =>
                (!string.IsNullOrEmpty(request.BurntImageFileName) &&
                    (img.FileRef.Equals(request.BurntImageFileName, StringComparison.OrdinalIgnoreCase) ||
                     Path.GetFileName(img.FileRef).Equals(request.BurntImageFileName, StringComparison.OrdinalIgnoreCase) ||
                     request.BurntImageFileName.Contains(img.ImageId.ToString(), StringComparison.OrdinalIgnoreCase))) ||
                (!string.IsNullOrEmpty(request.OriginalImageFileName) &&
                     request.OriginalImageFileName.Contains(img.ImageId.ToString(), StringComparison.OrdinalIgnoreCase)));

        }

        var imageId = existingImage?.ImageId ?? Guid.NewGuid();
        var baseName = $"case_{caseId}_img_{imageId}";
        var originalExt = Path.GetExtension(request.OriginalImageFileName);
        var burntExt = Path.GetExtension(request.BurntImageFileName);
        var burntPath = $"{baseName}{burntExt}";

        // Parse Annotations
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var aiBboxes = JsonSerializer.Deserialize<List<BBoxDto>>(request.AiPredictionsJson, options) ?? new List<BBoxDto>();
        var docBboxes = JsonSerializer.Deserialize<List<BBoxDto>>(request.DoctorAnnotationsJson, options) ?? new List<BBoxDto>();

        // Sort AI predictions descending by confidence (with coordinate tie-breaker for deterministic evaluation)
        var sortedAiBboxes = aiBboxes
            .OrderByDescending(b => b.Confidence)
            .ThenBy(b => b.Xmin)
            .ThenBy(b => b.Ymin)
            .ToList();

        // Generate YOLO text from docBboxes
        // Format: class_id x_center y_center width height
        var yoloLines = docBboxes.Select(b => 
        {
            var xCenter = (b.Xmin + b.Xmax) / 2;
            var yCenter = (b.Ymin + b.Ymax) / 2;
            var width = b.Xmax - b.Xmin;
            var height = b.Ymax - b.Ymin;
            return $"0 {xCenter:0.6f} {yCenter:0.6f} {width:0.6f} {height:0.6f}";
        }).ToList();
        var yoloText = string.Join("\n", yoloLines);

        // Upload to Storage
        await _storage.UploadAsync(request.OriginalImageStream, $"{baseName}{originalExt}", request.OriginalImageContentType, "datasets", ct);

        using var yoloStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(yoloText));
        await _storage.UploadAsync(yoloStream, $"{baseName}.txt", "text/plain", "datasets", ct);

        await _storage.UploadAsync(request.BurntImageStream, burntPath, request.BurntImageContentType, "ultrasound-images", ct);

        // Database Transaction
        using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            if (existingImage != null)
            {
                // Re-diagnosis / caliper update idempotence:
                // Calculate old metrics attributed to this image (filtering out sentinels with Confidence == 0m)
                var oldPredictions = await _db.AiPredictions
                    .Where(p => p.ImageId == imageId && p.Confidence > 0m)
                    .ToListAsync(ct);
                var oldAnnotations = await _db.DoctorAnnotations
                    .Where(a => a.ImageId == imageId)
                    .ToListAsync(ct);

                int oldTp = 0;
                int oldFp = 0;
                var matchedOldGtIndices = new HashSet<int>();

                var sortedOldPreds = oldPredictions
                    .OrderByDescending(p => p.Confidence)
                    .ThenBy(p => p.BboxXmin)
                    .ThenBy(p => p.BboxYmin)
                    .ToList();

                foreach (var pred in sortedOldPreds)
                {
                    decimal maxIou = 0;
                    int bestGtIndex = -1;

                    for (int i = 0; i < oldAnnotations.Count; i++)
                    {
                        if (matchedOldGtIndices.Contains(i)) continue;

                        var ann = oldAnnotations[i];
                        var iou = CalculateIoU(
                            pred.BboxXmin, pred.BboxYmin, pred.BboxXmax, pred.BboxYmax,
                            ann.BboxXmin, ann.BboxYmin, ann.BboxXmax, ann.BboxYmax);

                        if (iou > maxIou)
                        {
                            maxIou = iou;
                            bestGtIndex = i;
                        }
                    }

                    if (maxIou >= IoUCalculator.MatchThreshold && bestGtIndex >= 0)
                    {
                        oldTp++;
                        matchedOldGtIndices.Add(bestGtIndex);
                    }
                    else
                    {
                        oldFp++;
                    }
                }

                int oldFn = oldAnnotations.Count - matchedOldGtIndices.Count;

                // Deduct old metrics from active model
                activeModel.LiveTp = Math.Max(0, activeModel.LiveTp - oldTp);
                activeModel.LiveFp = Math.Max(0, activeModel.LiveFp - oldFp);
                activeModel.LiveFn = Math.Max(0, activeModel.LiveFn - oldFn);

                // Remove previous predictions (including sentinels) and annotations
                var allOldPredictions = await _db.AiPredictions
                    .Where(p => p.ImageId == imageId)
                    .ToListAsync(ct);
                _db.AiPredictions.RemoveRange(allOldPredictions);
                _db.DoctorAnnotations.RemoveRange(oldAnnotations);

                // Update existing ultrasound image entity
                var trackedImage = await _db.UltrasoundImages.FirstOrDefaultAsync(i => i.ImageId == imageId, ct);
                if (trackedImage != null)
                {
                    trackedImage.FileRef = burntPath;
                    trackedImage.Note = request.Note;
                    trackedImage.UploadedAt = DateTime.UtcNow;
                }
            }
            else
            {
                var ultrasoundImage = new UltrasoundImage
                {
                    ImageId = imageId,
                    CaseId = caseId,
                    FileRef = burntPath,
                    Note = request.Note,
                    UploadedAt = DateTime.UtcNow
                };
                await _images.AddRangeAsync(new[] { ultrasoundImage }, ct);
            }

            // Sentinel zero-prediction tracking: If AI predicted 0 boxes, insert sentinel record
            if (sortedAiBboxes.Count == 0)
            {
                var sentinel = new AiPrediction
                {
                    PredictionId = Guid.NewGuid(),
                    CaseId = caseId,
                    ImageId = imageId,
                    ModelVersionId = activeModelId,
                    BboxXmin = 0m,
                    BboxYmin = 0m,
                    BboxXmax = 0m,
                    BboxYmax = 0m,
                    Confidence = 0m,
                    CreatedAt = DateTime.UtcNow
                };
                await _predictions.AddRangeAsync(new[] { sentinel }, ct);
            }
            else
            {
                var aiPredictionEntities = sortedAiBboxes.Select(aiBox => new AiPrediction
                {
                    PredictionId = Guid.NewGuid(),
                    CaseId = caseId,
                    ImageId = imageId,
                    ModelVersionId = activeModelId,
                    BboxXmin = aiBox.Xmin,
                    BboxYmin = aiBox.Ymin,
                    BboxXmax = aiBox.Xmax,
                    BboxYmax = aiBox.Ymax,
                    Confidence = aiBox.Confidence,
                    CreatedAt = DateTime.UtcNow
                }).ToList();
                await _predictions.AddRangeAsync(aiPredictionEntities, ct);
            }

            var doctorAnnotationEntities = docBboxes.Select(docBox => new DoctorAnnotation
            {
                AnnotationId = Guid.NewGuid(),
                CaseId = caseId,
                ImageId = imageId,
                BboxXmin = docBox.Xmin,
                BboxYmin = docBox.Ymin,
                BboxXmax = docBox.Xmax,
                BboxYmax = docBox.Ymax,
                Source = "doctor_added",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();
            await _annotations.AddRangeAsync(doctorAnnotationEntities, ct);

            // Calculate O(1) Metrics for this image using greedy bipartite matching with fallback
            int newTp = 0;
            int newFp = 0;
            var matchedGtIndices = new HashSet<int>();

            foreach (var aiBox in sortedAiBboxes)
            {
                decimal maxIou = 0;
                int bestGtIndex = -1;

                for (int i = 0; i < docBboxes.Count; i++)
                {
                    if (matchedGtIndices.Contains(i)) continue;

                    var docBox = docBboxes[i];
                    var iou = CalculateIoU(
                        aiBox.Xmin, aiBox.Ymin, aiBox.Xmax, aiBox.Ymax,
                        docBox.Xmin, docBox.Ymin, docBox.Xmax, docBox.Ymax);

                    if (iou > maxIou)
                    {
                        maxIou = iou;
                        bestGtIndex = i;
                    }
                }

                if (maxIou >= IoUCalculator.MatchThreshold && bestGtIndex >= 0)
                {
                    newTp++;
                    matchedGtIndices.Add(bestGtIndex);
                }
                else
                {
                    newFp++;
                }
            }

            int newFn = docBboxes.Count - matchedGtIndices.Count;

            activeModel.LiveTp += newTp;
            activeModel.LiveFp += newFp;
            activeModel.LiveFn += newFn;

            await _aiModelVersionRepo.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            // Auto-add ULTRASOUND_EXAM (Khám siêu âm) service
            if (_caseClinicServiceService != null)
            {
                try
                {
                    await _caseClinicServiceService.AddServiceToCaseByCodeAsync(caseId, "ULTRASOUND_EXAM", ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tự động gắn dịch vụ ULTRASOUND_EXAM thất bại cho ca {CaseId}", caseId);
                }
            }
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private static decimal CalculateIoU(
        decimal xmin1, decimal ymin1, decimal xmax1, decimal ymax1,
        decimal xmin2, decimal ymin2, decimal xmax2, decimal ymax2)
    {
        var xA = Math.Max(xmin1, xmin2);
        var yA = Math.Max(ymin1, ymin2);
        var xB = Math.Min(xmax1, xmax2);
        var yB = Math.Min(ymax1, ymax2);

        var interArea = Math.Max(0m, xB - xA) * Math.Max(0m, yB - yA);

        var b1W = Math.Max(0m, xmax1 - xmin1);
        var b1H = Math.Max(0m, ymax1 - ymin1);
        var box1Area = b1W * b1H;

        var b2W = Math.Max(0m, xmax2 - xmin2);
        var b2H = Math.Max(0m, ymax2 - ymin2);
        var box2Area = b2W * b2H;

        var unionArea = box1Area + box2Area - interArea;
        if (unionArea <= 0m) return 0m;

        return Math.Min(1m, Math.Max(0m, interArea / unionArea));
    }
}
