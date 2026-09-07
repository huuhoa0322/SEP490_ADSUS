namespace ADSUS_BE.BLL.AIModelManagement.DTOs;

/// <summary>
/// Doctor-facing (UC-20: "Doctor sees the Active version's code/status only, UC-19").
/// Không chứa Metrics/Live confusion-matrix/RegisteredBy — các field đó chỉ dành cho Admin
/// qua <see cref="AiModelVersionDto"/> (P11 review Feature 6, 30/08/2026).
/// </summary>
public class ActiveAiModelVersionDto
{
    public string VersionCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
