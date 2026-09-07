using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.ExternalServices;
using ADSUS_BE.DAL.Repositories.Interfaces;
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
    private readonly ILogger<CaseDiagnosisService> _logger;
    private readonly string _aiBackendUrl;
    private readonly string? _aiBackendToken;

    public CaseDiagnosisService(
        AppDbContext db,
        IFileStorageService storage,
        IHttpClientFactory httpClientFactory,
        IAiModelVersionRepository aiModelVersionRepo,
        IUltrasoundImageRepository images,
        IAiPredictionRepository predictions,
        IDoctorAnnotationRepository annotations,
        IConfiguration configuration,
        ILogger<CaseDiagnosisService> logger)
    {
        _db = db;
        _storage = storage;
        _httpClientFactory = httpClientFactory;
        _aiModelVersionRepo = aiModelVersionRepo;
        _images = images;
        _predictions = predictions;
        _annotations = annotations;
        _logger = logger;

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

    public async Task<JsonElement> AnalyzeImageAsync(Guid caseId, Stream imageStream, string fileName, string contentType, CancellationToken ct = default)
    {
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
        try
        {
            response = await client.PostAsync($"{_aiBackendUrl}/api/detect", content, ct);
        }
        catch (HttpRequestException)
        {
            throw new BusinessException("Hệ thống AI Backend đang tắt hoặc không thể kết nối. Vui lòng bật AI Backend (hoặc cấu hình Ngrok) trước khi tải ảnh.");
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
        // 1. Create ImageId
        var imageId = Guid.NewGuid();
        var baseName = $"case_{caseId}_img_{imageId}";
        var originalExt = Path.GetExtension(request.OriginalImageFileName);
        var burntExt = Path.GetExtension(request.BurntImageFileName);
        
        // 2. Parse Annotations
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var aiBboxes = JsonSerializer.Deserialize<List<BBoxDto>>(request.AiPredictionsJson, options) ?? new List<BBoxDto>();
        var docBboxes = JsonSerializer.Deserialize<List<BBoxDto>>(request.DoctorAnnotationsJson, options) ?? new List<BBoxDto>();

        // 3. Generate YOLO text from docBboxes
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

        // 4. Upload to Supabase Storage
        await _storage.UploadAsync(request.OriginalImageStream, $"{baseName}{originalExt}", request.OriginalImageContentType, "datasets", ct);

        using var yoloStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(yoloText));
        await _storage.UploadAsync(yoloStream, $"{baseName}.txt", "text/plain", "datasets", ct);

        var burntPath = $"{baseName}{burntExt}";
        await _storage.UploadAsync(request.BurntImageStream, burntPath, request.BurntImageContentType, "ultrasound-images", ct);

        // 4.5 Fetch true active ModelVersionId for database tracking
        var activeModel = await _aiModelVersionRepo.GetActiveVersionReadOnlyAsync(ct);
        if (activeModel == null) throw new BusinessException("Hệ thống chưa có phiên bản AI nào được kích hoạt. Vui lòng liên hệ Admin.");
        var activeModelId = activeModel.ModelVersionId;

        // 5. Database Transaction
        using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
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

            var aiPredictionEntities = aiBboxes.Select(aiBox => new AiPrediction
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

            // Calculate O(1) Metrics for this single image
            int newTp = 0;
            int newFp = 0;
            int newFn = 0;

            var matchedGtIndices = new HashSet<int>();

            foreach (var aiBox in aiBboxes)
            {
                decimal maxIou = 0;
                int bestGtIndex = -1;

                for (int i = 0; i < docBboxes.Count; i++)
                {
                    var docBox = docBboxes[i];
                    var iou = IoUCalculator.Calculate(
                        aiBox.Xmin, aiBox.Ymin, aiBox.Xmax, aiBox.Ymax,
                        docBox.Xmin, docBox.Ymin, docBox.Xmax, docBox.Ymax);
                    
                    if (iou > maxIou)
                    {
                        maxIou = iou;
                        bestGtIndex = i;
                    }
                }

                if (maxIou >= IoUCalculator.MatchThreshold && !matchedGtIndices.Contains(bestGtIndex))
                {
                    newTp++;
                    matchedGtIndices.Add(bestGtIndex);
                }
                else
                {
                    newFp++;
                }
            }

            newFn = docBboxes.Count - matchedGtIndices.Count;

            activeModel.LiveTp += newTp;
            activeModel.LiveFp += newFp;
            activeModel.LiveFn += newFn;

            await _aiModelVersionRepo.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
