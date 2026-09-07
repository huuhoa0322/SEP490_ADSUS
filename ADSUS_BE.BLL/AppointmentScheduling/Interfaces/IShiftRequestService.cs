using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.AppointmentScheduling.Interfaces;

public interface IShiftRequestService
{
    Task<ShiftRequestResponse> CreateRequestAsync(Guid userId, CreateShiftRequestDto dto, CancellationToken ct = default);
    
    Task<PagedResult<ShiftRequestResponse>> ListMyRequestsAsync(Guid userId, ShiftRequestStatus? status, int page, int pageSize, CancellationToken ct = default);
    
    Task<PagedResult<ShiftRequestResponse>> ListAllRequestsAsync(ShiftRequestStatus? status, Guid? doctorId, int page, int pageSize, CancellationToken ct = default);
    
    Task<ShiftRequestResponse> ReviewRequestAsync(Guid requestId, Guid adminId, ReviewShiftRequestDto dto, CancellationToken ct = default);
    
    Task<List<DayShiftSummary>> GetMonthSummaryAsync(Guid userId, int year, int month, CancellationToken ct = default);
}
