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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ADSUS_BE.BLL.MedicalRecord.Services;

public sealed class CaseDiagnosisService : ICaseDiagnosisService
{
    private readonly AppDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAiModelVersionRepository _aiModelVersionRepo;
    private readonly string _aiBackendUrl;

    public CaseDiagnosisService(
        AppDbContext db, 
        IFileStorageService storage, 
        IHttpClientFactory httpClientFactory, 
        IAiModelVersionRepository aiModelVersionRepo,
        IConfiguration configuration)
    {
        _db = db;
        _storage = storage;
        _httpClientFactory = httpClientFactory;
        _aiModelVersionRepo = aiModelVersionRepo;
        _aiBackendUrl = configuration["AiBackend:WebhookUrl"] ?? "http://localhost:8000";
    }

    public async Task<JsonElement> AnalyzeImageAsync(Guid caseId, Stream imageStream, string fileName, string contentType, CancellationToken ct = default)
    {
        // Ignore the modelVersionId passed from frontend and fetch the true ACTIVE model
        var activeModel = await _aiModelVersionRepo.GetActiveVersionAsync(ct);
        if (activeModel == null) throw new InvalidOperationException("Hệ thống chưa có phiên bản AI nào được kích hoạt. Vui lòng liên hệ Admin.");

        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(2); // AI might take time

        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(imageStream);
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        content.Add(streamContent, "file", fileName);
        
        content.Add(new StringContent(activeModel.HfRepoId), "repo_id");
        content.Add(new StringContent(activeModel.HfFilename), "filename");
        
        // Send to Python backend which uses its currently loaded model
        var response = await client.PostAsync($"{_aiBackendUrl}/api/detect", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"AI Backend Error: {error}");
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
        var activeModel = await _aiModelVersionRepo.GetActiveVersionAsync(ct);
        if (activeModel == null) throw new InvalidOperationException("Hệ thống chưa có phiên bản AI nào được kích hoạt. Vui lòng liên hệ Admin.");
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
            _db.UltrasoundImages.Add(ultrasoundImage);

            foreach (var aiBox in aiBboxes)
            {
                _db.AiPredictions.Add(new AiPrediction
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
                });
            }

            foreach (var docBox in docBboxes)
            {
                _db.DoctorAnnotations.Add(new DoctorAnnotation
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
                });
            }

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

                if (maxIou >= 0.5m && !matchedGtIndices.Contains(bestGtIndex))
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
            
            _db.AiModelVersions.Update(activeModel);

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
