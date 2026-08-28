using ADSUS_BE.BLL.Engagement.Services;

namespace ADSUS_BE.BLL.Engagement.Interfaces;

/// <summary>
/// Detects patient intent from chat message to enable selective data source querying.
/// Reduces latency and token bloat by only fetching relevant data sources.
/// </summary>
public interface IIntentDetector
{
    /// <summary>
    /// Detect intent from user message.
    /// </summary>
    /// <param name="message">The patient's chat message.</param>
    /// <param name="ct">CancellationToken.</param>
    Task<IntentResult> DetectAsync(string? message, CancellationToken ct = default);
}
