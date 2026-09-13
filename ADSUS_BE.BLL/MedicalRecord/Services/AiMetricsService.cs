using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.DAL.Repositories.Interfaces;

namespace ADSUS_BE.BLL.MedicalRecord.Services;

/// <summary>
/// Đọc qua Repository thay vì AppDbContext trực tiếp (P11 review Feature 4, 29/08/2026) —
/// GetByIdAsync/SaveChangesAsync của IAiModelVersionRepository dùng chung 1 DbContext scoped
/// với 2 repository kia, nên vẫn track/save đúng entity model đang sửa (LiveMap50,
/// LastEvaluatedAt) như code cũ gọi _db.SaveChangesAsync() trực tiếp.
/// </summary>
public sealed class AiMetricsService : IAiMetricsService
{
    private readonly IAiModelVersionRepository _modelVersions;
    private readonly IAiPredictionRepository _predictions;
    private readonly IDoctorAnnotationRepository _annotations;

    public AiMetricsService(
        IAiModelVersionRepository modelVersions,
        IAiPredictionRepository predictions,
        IDoctorAnnotationRepository annotations)
    {
        _modelVersions = modelVersions;
        _predictions = predictions;
        _annotations = annotations;
    }

    public async Task CalculateMap50Async(Guid modelVersionId, CancellationToken ct = default)
    {
        var model = await _modelVersions.GetByIdAsync(modelVersionId, ct);
        if (model == null) throw new InvalidOperationException("Model version not found");

        // 1. Fetch all predictions and GTs for this model version (including sentinels with Confidence == 0m)
        var allPredictions = await _predictions.ListByModelVersionAsync(modelVersionId, ct);

        // Capture all evaluated image IDs (including 0-prediction images tracked via sentinels)
        var evaluatedImageIds = allPredictions.Select(p => p.ImageId).Distinct().ToList();

        var annotations = await _annotations.ListByImageIdsAsync(evaluatedImageIds, ct);

        int totalGt = annotations.Count;

        // Filter real predictions (exclude sentinel records where Confidence == 0m)
        var validPredictions = allPredictions
            .Where(p => p.Confidence > 0m)
            .OrderByDescending(p => p.Confidence)
            .ThenBy(p => p.PredictionId)
            .ToList();

        if (totalGt == 0 || validPredictions.Count == 0)
        {
            model.LiveTp = 0;
            model.LiveFp = validPredictions.Count;
            model.LiveFn = totalGt;
            model.LiveMap50 = 0m;
            model.LastEvaluatedAt = DateTime.UtcNow;
            await _modelVersions.SaveChangesAsync(ct);
            return;
        }

        // 2. Group GTs by ImageId and track matching status
        var gtDict = annotations
            .GroupBy(a => a.ImageId)
            .ToDictionary(g => g.Key, g => g.Select(a => new GtInfo { Box = a, IsMatched = false }).ToList());

        var tpList = new List<int>(); // 1 for TP, 0 for FP

        // 3. Match predictions using greedy bipartite matching with fallback
        foreach (var pred in validPredictions)
        {
            if (!gtDict.TryGetValue(pred.ImageId, out var gtsForImage))
            {
                tpList.Add(0); // FP because no GT for this image
                continue;
            }

            decimal maxIou = 0;
            int bestGtIndex = -1;

            for (int i = 0; i < gtsForImage.Count; i++)
            {
                var gtInfo = gtsForImage[i];
                // Greedy Fallback: Skip ground truths already claimed by higher-confidence predictions
                if (gtInfo.IsMatched) continue;

                var iou = CalculateIoU(
                    pred.BboxXmin, pred.BboxYmin, pred.BboxXmax, pred.BboxYmax,
                    gtInfo.Box.BboxXmin, gtInfo.Box.BboxYmin, gtInfo.Box.BboxXmax, gtInfo.Box.BboxYmax);

                if (iou > maxIou)
                {
                    maxIou = iou;
                    bestGtIndex = i;
                }
            }

            if (maxIou >= IoUCalculator.MatchThreshold && bestGtIndex >= 0)
            {
                tpList.Add(1);
                gtsForImage[bestGtIndex].IsMatched = true;
            }
            else
            {
                tpList.Add(0);
            }
        }

        // 4. Calculate running precision and recall
        var precisions = new decimal[tpList.Count];
        var recalls = new decimal[tpList.Count];
        int accTp = 0;
        int accFp = 0;

        for (int i = 0; i < tpList.Count; i++)
        {
            if (tpList[i] == 1) accTp++;
            else accFp++;

            precisions[i] = (decimal)accTp / (accTp + accFp);
            recalls[i] = (decimal)accTp / totalGt;
        }

        // 5. Calculate mAP50 using every-point interpolation (VOC 2012)
        // Make precision monotonically decreasing
        for (int i = precisions.Length - 2; i >= 0; i--)
        {
            precisions[i] = Math.Max(precisions[i], precisions[i + 1]);
        }

        // 6. Continuous rectangular area integration
        decimal map50 = 0m;
        decimal prevRecall = 0m;
        for (int i = 0; i < tpList.Count; i++)
        {
            decimal deltaRecall = recalls[i] - prevRecall;
            map50 += precisions[i] * deltaRecall;
            prevRecall = recalls[i];
        }

        // 7. Full live metrics resynchronization from database history
        int totalTp = accTp;
        int totalFp = accFp;
        int totalFn = Math.Max(0, totalGt - totalTp);

        model.LiveTp = totalTp;
        model.LiveFp = totalFp;
        model.LiveFn = totalFn;
        model.LiveMap50 = Math.Round(map50 * 100m, 2);
        model.LastEvaluatedAt = DateTime.UtcNow;

        await _modelVersions.SaveChangesAsync(ct);
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

    private class GtInfo
    {
        public DAL.Entities.DoctorAnnotation Box { get; set; } = null!;
        public bool IsMatched { get; set; }
    }
}
