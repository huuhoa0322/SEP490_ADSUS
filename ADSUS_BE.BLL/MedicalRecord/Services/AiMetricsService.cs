using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.DAL.Data;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.BLL.MedicalRecord.Services;

public sealed class AiMetricsService : IAiMetricsService
{
    private readonly AppDbContext _db;

    public AiMetricsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task CalculateMap50Async(Guid modelVersionId, CancellationToken ct = default)
    {
        var model = await _db.AiModelVersions.FirstOrDefaultAsync(m => m.ModelVersionId == modelVersionId, ct);
        if (model == null) throw new InvalidOperationException("Model version not found");

        // 1. Fetch all predictions and GTs for this model
        // Note: DoctorAnnotations don't have ModelVersionId directly, but we can fetch based on ImageId that the model evaluated, 
        // or just fetch all DoctorAnnotations. Actually, the most accurate is to fetch DoctorAnnotations for Images where this model made predictions.
        
        var predictions = await _db.AiPredictions
            .Where(p => p.ModelVersionId == modelVersionId)
            .ToListAsync(ct);

        var imageIds = predictions.Select(p => p.ImageId).Distinct().ToList();

        var annotations = await _db.DoctorAnnotations
            .Where(a => imageIds.Contains(a.ImageId))
            .ToListAsync(ct);

        int totalGt = annotations.Count;

        if (totalGt == 0 || predictions.Count == 0)
        {
            model.LiveMap50 = 0;
            model.LastEvaluatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return;
        }

        // 2. Group GTs by ImageId and track matching status
        var gtDict = annotations
            .GroupBy(a => a.ImageId)
            .ToDictionary(g => g.Key, g => g.Select(a => new GtInfo { Box = a, IsMatched = false }).ToList());

        // 3. Sort all predictions by confidence descending
        var sortedPreds = predictions.OrderByDescending(p => p.Confidence).ToList();
        
        var tpList = new List<int>(); // 1 for TP, 0 for FP

        // 4. Match predictions
        foreach (var pred in sortedPreds)
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
                var iou = IoUCalculator.Calculate(
                    pred.BboxXmin, pred.BboxYmin, pred.BboxXmax, pred.BboxYmax,
                    gtInfo.Box.BboxXmin, gtInfo.Box.BboxYmin, gtInfo.Box.BboxXmax, gtInfo.Box.BboxYmax);
                
                if (iou > maxIou)
                {
                    maxIou = iou;
                    bestGtIndex = i;
                }
            }

            if (maxIou >= 0.5m && bestGtIndex >= 0 && !gtsForImage[bestGtIndex].IsMatched)
            {
                tpList.Add(1);
                gtsForImage[bestGtIndex].IsMatched = true;
            }
            else
            {
                tpList.Add(0);
            }
        }

        // 5. Calculate PR curve
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

        // 6. Calculate mAP50 using every-point interpolation (VOC 2012)
        // Make precision monotonically decreasing
        for (int i = precisions.Length - 2; i >= 0; i--)
        {
            precisions[i] = Math.Max(precisions[i], precisions[i + 1]);
        }

        decimal map50 = 0;
        decimal prevRecall = 0;
        for (int i = 0; i < tpList.Count; i++)
        {
            decimal deltaRecall = recalls[i] - prevRecall;
            map50 += precisions[i] * deltaRecall;
            prevRecall = recalls[i];
        }

        model.LiveMap50 = map50 * 100; // Store as percentage 0-100
        model.LastEvaluatedAt = DateTime.UtcNow;
        
        await _db.SaveChangesAsync(ct);
    }

    private class GtInfo
    {
        public DAL.Entities.DoctorAnnotation Box { get; set; } = null!;
        public bool IsMatched { get; set; }
    }
}
