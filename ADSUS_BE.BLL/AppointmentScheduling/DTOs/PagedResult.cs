namespace ADSUS_BE.BLL.AppointmentScheduling.DTOs;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);