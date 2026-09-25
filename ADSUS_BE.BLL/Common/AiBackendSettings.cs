namespace ADSUS_BE.BLL.Common;

public class AiBackendSettings
{
    public const string SectionName = "AiBackend";

    public string WebhookUrl { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// OpenAI API key cho Chatbot (gpt-4o-mini).
    /// Mặc định rỗng → dùng FakeChatClient.
    /// </summary>
    public string OpenAiApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model cho Chatbot. Default: gpt-4o-mini.
    /// </summary>
    public string OpenAiModel { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// System prompt cho Chatbot (load từ config hoặc dùng constant).
    /// </summary>
    public string ChatBotSystemPrompt { get; set; } = string.Empty;
}
