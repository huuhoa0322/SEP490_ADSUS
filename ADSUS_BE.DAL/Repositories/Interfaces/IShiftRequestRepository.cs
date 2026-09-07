using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

public interface IShiftRequestRepository
{
    Task<ShiftRequest?> GetByIdAsync(Guid requestId, CancellationToken ct = default);
    Task<(IReadOnlyList<ShiftRequest> Items, int Total)> ListAsync(
        Guid? userId, ShiftRequestStatus? status,
        int page, int pageSize, CancellationToken ct = default);
    Task<bool> HasActiveRequestAsync(
        Guid userId, DateOnly date, ShiftType shiftType,
        ShiftRequestType requestType, CancellationToken ct = default);
    Task<List<ShiftRequest>> ListByUserMonthAsync(
        Guid userId, DateOnly monthStart, DateOnly monthEnd, CancellationToken ct = default);
    Task AddAsync(ShiftRequest entity, CancellationToken ct = default);
    Task UpdateAsync(ShiftRequest entity, CancellationToken ct = default);
}
