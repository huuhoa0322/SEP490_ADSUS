using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ADSUS_BE.BLL.MedicalRecord.DTOs;

namespace ADSUS_BE.BLL.MedicalRecord.Interfaces;

public interface ICaseDiagnosisService
{
    Task<JsonElement> AnalyzeImageAsync(Guid caseId, Guid modelVersionId, Stream imageStream, string fileName, string contentType, CancellationToken ct = default);
    Task ConfirmAnalysisAsync(Guid caseId, ConfirmAnalysisRequest request, CancellationToken ct = default);
}
