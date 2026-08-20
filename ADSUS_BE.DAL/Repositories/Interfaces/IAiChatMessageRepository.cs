using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Repository cho AiChatMessage. GB-03: KHÔNG có Remove/Delete.
/// Chat log không bị xóa cứng.
/// </summary>
public interface IAiChatMessageRepository
{
    /// <summary>Thêm một tin nhắn mới (USER hoặc ASSISTANT).</summary>
    Task<AiChatMessage> AddAsync(AiChatMessage message, CancellationToken ct = default);

    /// <summary>
    /// Lấy lịch sử hội thoại của một user, paginate theo khoảng thời gian.
    /// Kết quả sắp xếp DESC theo created_at (mới nhất trước).
    /// </summary>
    /// <param name="userId">ID tài khoản (từ JWT, không tin query/client).</param>
    /// <param name="from">Lọc từ thời điểm này (inclusive).</param>
    /// <param name="to">Lọc đến thời điểm này (inclusive).</param>
    /// <param name="limit">Số bản ghi tối đa trả về.</param>
    /// <param name="ct">CancellationToken.</param>
    Task<IReadOnlyList<AiChatMessage>> ListByUserAsync(
        Guid userId,
        DateTime from,
        DateTime to,
        int limit,
        CancellationToken ct = default);
}
