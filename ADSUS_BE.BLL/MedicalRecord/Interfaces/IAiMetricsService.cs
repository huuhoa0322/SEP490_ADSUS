using System;
using System.Threading;
using System.Threading.Tasks;

namespace ADSUS_BE.BLL.MedicalRecord.Interfaces;

public interface IAiMetricsService
{
    Task CalculateMap50Async(Guid modelVersionId, CancellationToken ct = default);
}
