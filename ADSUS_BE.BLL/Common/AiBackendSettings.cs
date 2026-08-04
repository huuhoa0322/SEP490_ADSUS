namespace ADSUS_BE.BLL.Common;

public class AiBackendSettings
{
    public const string SectionName = "AiBackend";

    public string WebhookUrl { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}
