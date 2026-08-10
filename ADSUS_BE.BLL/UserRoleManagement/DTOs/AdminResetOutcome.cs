namespace ADSUS_BE.BLL.UserRoleManagement.DTOs;

/// <summary>
/// Kết quả của AdminResetAsync (UC-03 AF-02 / UC-06 AF-03).
///
/// TemporaryPassword chỉ khác null đúng MỘT trường hợp: tài khoản không có email (quyết định
/// ghi đè 06/08/2026) — khi đó không còn chỗ nào để gửi thư, nên trả plaintext MỘT LẦN để
/// người thao tác (Admin/Điều dưỡng) đọc trực tiếp cho chủ tài khoản. Có email thì vẫn gửi âm
/// thầm như cũ, TemporaryPassword luôn null trong trường hợp đó.
/// </summary>
public sealed record AdminResetOutcome(AccountOperationResult Result, string? TemporaryPassword);
