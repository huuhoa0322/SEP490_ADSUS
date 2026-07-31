namespace ADSUS_BE.BLL.Common;

/// <summary>
/// Wraps a page of results, following the team's api_design_rules pagination contract
/// (L2 §7): { "items", "page", "pageSize", "totalItems", "totalPages" }.
/// </summary>
public record PagedResult<T>(List<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);
