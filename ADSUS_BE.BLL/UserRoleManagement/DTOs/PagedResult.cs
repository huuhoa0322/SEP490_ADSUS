namespace ADSUS_BE.BLL.UserRoleManagement.DTOs;

/// <summary>
/// Một trang kết quả kèm tổng số bản ghi, để giao diện dựng được thanh phân trang.
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    /// <summary>Tổng số trang, luôn ít nhất là 1 để giao diện không phải xử lý trường hợp 0.</summary>
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
