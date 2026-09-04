using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ADSUS_BE.BLL.AppointmentScheduling.Services;

public class ShiftRequestService : IShiftRequestService
{
    private readonly IShiftRequestRepository _repo;
    private readonly IUserRepository _userRepo;
    private readonly AppDbContext _db;
    private readonly INotificationService _notificationService;
    private readonly int _minAdvanceDays;

    public ShiftRequestService(
        IShiftRequestRepository repo,
        IUserRepository userRepo,
        AppDbContext db,
        INotificationService notificationService,
        IConfiguration config)
    {
        _repo = repo;
        _userRepo = userRepo;
        _db = db;
        _notificationService = notificationService;
        _minAdvanceDays = config.GetValue<int>("ScheduleSettings:MinAdvanceDaysForLeave", 2);
    }

    public async Task<ShiftRequestResponse> CreateRequestAsync(Guid userId, CreateShiftRequestDto dto, CancellationToken ct = default)
    {
        var doctor = await _userRepo.GetByIdAsync(userId, ct);
        if (doctor is null || doctor.Role != UserRole.Doctor)
        {
            throw new InvalidOperationException("User is not a valid Doctor.");
        }

        // Validate ShiftType
        if (dto.RequestType == ShiftRequestType.Overtime && dto.ShiftType != ShiftType.Evening)
        {
            throw new InvalidOperationException("Ca tăng ca chỉ được chọn Ca Tối.");
        }
        if (dto.RequestType == ShiftRequestType.Leave && dto.ShiftType == ShiftType.Evening)
        {
            throw new InvalidOperationException("Không thể xin nghỉ Ca Tối (đây là ca tăng thêm).");
        }
        if (dto.RequestType == ShiftRequestType.Overtime && dto.ShiftType == ShiftType.FullDay)
        {
            throw new InvalidOperationException("Không thể chọn Cả ngày cho yêu cầu tăng ca.");
        }

        // Advance notice validation
        var minDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(_minAdvanceDays);
        if (dto.RequestDate < minDate)
        {
            throw new InvalidOperationException($"Phải gửi yêu cầu trước ít nhất {_minAdvanceDays} ngày.");
        }

        // Duplicate validation
        var hasActive = await _repo.HasActiveRequestAsync(userId, dto.RequestDate, dto.ShiftType, dto.RequestType, ct);
        if (hasActive)
        {
            if (dto.ShiftType == ShiftType.FullDay)
            {
                throw new InvalidOperationException("Bạn đã có yêu cầu cho Ca Sáng hoặc Ca Chiều trong ngày này. Để xin nghỉ Cả ngày, hãy hủy yêu cầu cũ trước.");
            }
            throw new InvalidOperationException("Bạn đã gửi yêu cầu cho ca này, hoặc đã có yêu cầu nghỉ Cả ngày rồi.");
        }

        var entity = new ShiftRequest
        {
            UserId = userId,
            RequestType = dto.RequestType,
            RequestDate = dto.RequestDate,
            ShiftType = dto.ShiftType,
            Reason = dto.Reason,
            Status = ShiftRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repo.AddAsync(entity, ct);
        
        // Gửi Notification cho tất cả Admin
        var admins = await _db.Users.Where(u => u.Role == UserRole.Admin).ToListAsync(ct);
        foreach (var admin in admins)
        {
            var notification = new SendNotificationRequest
            {
                UserId = admin.UserId,
                Type = "shift_request_new",
                Title = "Yêu cầu thay đổi lịch làm việc",
                Body = $"Bác sĩ {doctor.FullName} vừa gửi yêu cầu {(dto.RequestType == ShiftRequestType.Leave ? "Xin nghỉ" : "Tăng ca")} cho ngày {dto.RequestDate:dd/MM/yyyy}.",
                DeepLink = "/admin/shift-requests"
            };
            await _notificationService.SendAsync(notification, ct);
        }
        
        // Return mapped
        return MapToResponse(entity, doctor);
    }

    public async Task<PagedResult<ShiftRequestResponse>> ListMyRequestsAsync(Guid userId, ShiftRequestStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var (items, total) = await _repo.ListAsync(userId, status, page, pageSize, ct);
        var mapped = items.Select(i => MapToResponse(i, i.User)).ToList();
        return new PagedResult<ShiftRequestResponse>(mapped, page, pageSize, total, (int)Math.Ceiling((double)total / pageSize));
    }

    public async Task<PagedResult<ShiftRequestResponse>> ListAllRequestsAsync(ShiftRequestStatus? status, Guid? doctorId, int page, int pageSize, CancellationToken ct = default)
    {
        var (items, total) = await _repo.ListAsync(doctorId, status, page, pageSize, ct);
        var mapped = items.Select(i => MapToResponse(i, i.User)).ToList();
        return new PagedResult<ShiftRequestResponse>(mapped, page, pageSize, total, (int)Math.Ceiling((double)total / pageSize));
    }

    public async Task<ShiftRequestResponse> ReviewRequestAsync(Guid requestId, Guid adminId, ReviewShiftRequestDto dto, CancellationToken ct = default)
    {
        var request = await _repo.GetByIdAsync(requestId, ct);
        if (request is null)
        {
            throw new InvalidOperationException($"Request '{requestId}' not found.");
        }
        if (request.Status != ShiftRequestStatus.Pending)
        {
            throw new InvalidOperationException("Yêu cầu này đã được xử lý trước đó.");
        }

        if (dto.Decision.Equals("REJECTED", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(dto.RejectReason))
            {
                throw new InvalidOperationException("Vui lòng nhập lý do từ chối.");
            }
            request.Status = ShiftRequestStatus.Rejected;
            request.RejectReason = dto.RejectReason;
        }
        else if (dto.Decision.Equals("APPROVED", StringComparison.OrdinalIgnoreCase))
        {
            request.Status = ShiftRequestStatus.Approved;
            
            // Handle Side-Effects
            if (request.RequestType == ShiftRequestType.Leave)
            {
                await HandleLeaveApprovalAsync(request, ct);
            }
            else if (request.RequestType == ShiftRequestType.Overtime)
            {
                await HandleOvertimeApprovalAsync(request, ct);
            }
        }
        else
        {
            throw new InvalidOperationException("Quyết định không hợp lệ (chỉ nhận APPROVED hoặc REJECTED).");
        }

        request.ReviewedBy = adminId;
        request.ReviewedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(request, ct);

        // Gửi Notification cho Bác sĩ
        var statusStr = request.Status == ShiftRequestStatus.Approved ? "ĐƯỢC DUYỆT" : "TỪ CHỐI";
        var body = $"Yêu cầu {(request.RequestType == ShiftRequestType.Leave ? "Xin nghỉ" : "Tăng ca")} ngày {request.RequestDate:dd/MM/yyyy} của bạn đã {statusStr.ToLower()}.";
        if (request.Status == ShiftRequestStatus.Rejected && !string.IsNullOrWhiteSpace(request.RejectReason)) 
        {
            body += $" Lý do: {request.RejectReason}";
        }

        var notification = new SendNotificationRequest
        {
            UserId = request.UserId,
            Type = "shift_request_reviewed",
            Title = $"Yêu cầu {statusStr}",
            Body = body,
            DeepLink = "/schedule"
        };
        await _notificationService.SendAsync(notification, ct);

        // Fetch again to include reviewer details
        var updated = await _repo.GetByIdAsync(requestId, ct);
        return MapToResponse(updated!, updated!.User);
    }

    private async Task HandleLeaveApprovalAsync(ShiftRequest request, CancellationToken ct)
    {
        var timeRanges = GetTimeRangesForShift(request.ShiftType);
        var now = DateTime.UtcNow;

        foreach (var range in timeRanges)
        {
            // Tìm tất cả slot trong range của Doctor trong ngày đó
            var slotsToClose = await _db.ScheduleSlots
                .Include(s => s.Appointments)
                .Where(s => s.DoctorId == request.UserId &&
                            s.SlotDate == request.RequestDate &&
                            s.StartTime >= range.Start &&
                            s.EndTime <= range.End &&
                            s.Status != SlotStatus.Closed)
                .ToListAsync(ct);

            foreach (var slot in slotsToClose)
            {
                slot.Status = SlotStatus.Closed;
                slot.UpdatedAt = now;

                // Handle BOOKED slot -> Cancel appointments (Phương án B)
                var activeAppointments = slot.Appointments.Where(a => a.Status == AppointmentStatus.Booked).ToList();
                foreach (var appointment in activeAppointments)
                {
                    appointment.Status = AppointmentStatus.Cancelled;
                    appointment.CancelledReason = "Bác sĩ nghỉ phép, lịch hẹn đã được hệ thống tự động hủy.";
                    appointment.UpdatedAt = now;

                    // Lấy user_id của bệnh nhân để gửi thông báo
                    var patientUserId = await _db.PatientProfiles
                        .Where(p => p.PatientProfileId == appointment.PatientProfileId)
                        .Select(p => p.UserId)
                        .FirstOrDefaultAsync(ct);

                    if (patientUserId != Guid.Empty)
                    {
                        var notification = new SendNotificationRequest
                        {
                            UserId = patientUserId,
                            Type = "appointment_cancellation",
                            Title = "Lịch khám đã bị hủy",
                            Body = $"Lịch khám lúc {slot.StartTime:hh\\:mm} ngày {slot.SlotDate:dd/MM/yyyy} đã bị hủy do bác sĩ có việc đột xuất. Xin lỗi vì sự bất tiện này.",
                            DeepLink = "/appointments/history"
                        };
                        // Lửa fire and forget hoặc await đều được, ở đây await.
                        await _notificationService.SendAsync(notification, ct);
                    }
                }
            }
        }
        
        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleOvertimeApprovalAsync(ShiftRequest request, CancellationToken ct)
    {
        // 17h đến 20h = 6 ca x 30 phút
        var now = DateTime.UtcNow;
        var existingSlots = await _db.ScheduleSlots
            .Where(s => s.DoctorId == request.UserId && s.SlotDate == request.RequestDate)
            .ToListAsync(ct);

        for (int i = 0; i < 6; i++)
        {
            var start = new TimeOnly(17, 0).AddMinutes(i * 30);
            var end = start.AddMinutes(30);

            var hasOverlap = existingSlots.Any(s => s.StartTime < end && start < s.EndTime);
            if (hasOverlap) continue;

            var slot = new ScheduleSlot
            {
                SlotId = Guid.NewGuid(),
                DoctorId = request.UserId,
                SlotDate = request.RequestDate,
                StartTime = start,
                EndTime = end,
                Status = SlotStatus.Open,
                CreatedAt = now,
                UpdatedAt = now,
            };
            await _db.ScheduleSlots.AddAsync(slot, ct);
        }
        
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<DayShiftSummary>> GetMonthSummaryAsync(Guid userId, int year, int month, CancellationToken ct = default)
    {
        var startDate = new DateOnly(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var slots = await _db.ScheduleSlots
            .Include(s => s.Appointments)
            .Where(s => s.DoctorId == userId && s.SlotDate >= startDate && s.SlotDate <= endDate)
            .ToListAsync(ct);

        var requests = await _repo.ListByUserMonthAsync(userId, startDate, endDate, ct);

        var results = new List<DayShiftSummary>();
        var nowLocal = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)); // Giả sử GMT+7

        for (var day = startDate; day <= endDate; day = day.AddDays(1))
        {
            var daySlots = slots.Where(s => s.SlotDate == day).ToList();
            var dayRequests = requests.Where(r => r.RequestDate == day).ToList();

            results.Add(new DayShiftSummary
            {
                Date = day,
                Morning = ComputeShiftInfo(day, ShiftType.Morning, daySlots, dayRequests, nowLocal),
                Afternoon = ComputeShiftInfo(day, ShiftType.Afternoon, daySlots, dayRequests, nowLocal),
                Evening = ComputeShiftInfo(day, ShiftType.Evening, daySlots, dayRequests, nowLocal, isEvening: true)
            });
        }

        return results;
    }

    private ShiftInfo? ComputeShiftInfo(
        DateOnly day, ShiftType shiftType, 
        List<ScheduleSlot> daySlots, 
        List<ShiftRequest> dayRequests,
        DateOnly nowLocal,
        bool isEvening = false)
    {
        var ranges = GetTimeRangesForShift(shiftType);
        var slotsInShift = daySlots.Where(s => ranges.Any(r => s.StartTime >= r.Start && s.EndTime <= r.End)).ToList();
        
        // Nếu là ca Tối và không có slot nào + không có request nào, trả về null (trống, không hiện ô Ca Tối)
        var relevantRequests = dayRequests.Where(r => r.ShiftType == shiftType || r.ShiftType == ShiftType.FullDay).ToList();
        
        if (isEvening && !slotsInShift.Any() && !relevantRequests.Any())
        {
            return null; 
        }

        var total = slotsInShift.Count;
        var closed = slotsInShift.Count(s => s.Status == SlotStatus.Closed);
        var booked = slotsInShift.Count(s => s.Appointments.Any(a => a.Status == AppointmentStatus.Booked));

        var pendingReq = relevantRequests.FirstOrDefault(r => r.Status == ShiftRequestStatus.Pending);

        string status;
        if (day < nowLocal)
        {
            status = "PAST";
        }
        else if (relevantRequests.Any(r => r.Status == ShiftRequestStatus.Approved && r.RequestType == ShiftRequestType.Leave))
        {
            status = "OFF";
        }
        else if (total > 0 && closed == total)
        {
            status = "OFF";
        }
        else if (booked > 0)
        {
            status = "HAS_BOOKINGS";
        }
        else if (total > 0)
        {
            status = "WORKING";
        }
        else 
        {
            // Trống slot nhưng không có request nghỉ (ví dụ: ngày lễ hoặc chưa default slot)
            status = "OFF"; 
        }

        return new ShiftInfo
        {
            TotalSlots = total,
            ClosedSlots = closed,
            BookedSlots = booked,
            Status = status,
            PendingRequestType = pendingReq?.RequestType
        };
    }

    private List<(TimeOnly Start, TimeOnly End)> GetTimeRangesForShift(ShiftType type)
    {
        return type switch
        {
            ShiftType.Morning => new List<(TimeOnly, TimeOnly)> { (new TimeOnly(8, 0), new TimeOnly(12, 0)) },
            ShiftType.Afternoon => new List<(TimeOnly, TimeOnly)> { (new TimeOnly(13, 0), new TimeOnly(17, 0)) },
            ShiftType.Evening => new List<(TimeOnly, TimeOnly)> { (new TimeOnly(17, 0), new TimeOnly(20, 0)) },
            ShiftType.FullDay => new List<(TimeOnly, TimeOnly)> { (new TimeOnly(8, 0), new TimeOnly(12, 0)), (new TimeOnly(13, 0), new TimeOnly(17, 0)) },
            _ => new List<(TimeOnly, TimeOnly)>()
        };
    }

    private ShiftRequestResponse MapToResponse(ShiftRequest req, User doctor)
    {
        return new ShiftRequestResponse
        {
            RequestId = req.RequestId,
            UserId = req.UserId,
            DoctorName = doctor?.FullName ?? "Unknown",
            RequestType = req.RequestType,
            RequestDate = req.RequestDate,
            ShiftType = req.ShiftType,
            ShiftLabel = GetShiftLabel(req.ShiftType),
            Reason = req.Reason,
            Status = req.Status,
            ReviewedByName = req.ReviewedByNavigation?.FullName,
            ReviewedAt = req.ReviewedAt,
            RejectReason = req.RejectReason,
            CreatedAt = req.CreatedAt
        };
    }

    private string GetShiftLabel(ShiftType type)
    {
        return type switch
        {
            ShiftType.Morning => "Ca Sáng",
            ShiftType.Afternoon => "Ca Chiều",
            ShiftType.Evening => "Ca Tối",
            ShiftType.FullDay => "Cả ngày",
            _ => type.ToString()
        };
    }
}
