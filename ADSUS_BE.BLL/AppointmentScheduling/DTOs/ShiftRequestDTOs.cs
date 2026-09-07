using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.AppointmentScheduling.DTOs;

public class CreateShiftRequestDto
{
    [Required(ErrorMessage = "Vui lòng chọn loại yêu cầu.")]
    public ShiftRequestType RequestType { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày.")]
    public DateOnly RequestDate { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ca.")]
    public ShiftType ShiftType { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do.")]
    [StringLength(500, ErrorMessage = "Lý do không được vượt quá 500 ký tự.")]
    public string Reason { get; set; } = null!;
}

public class ReviewShiftRequestDto
{
    [Required] 
    public string Decision { get; set; } = null!; // "APPROVED" | "REJECTED"
    
    [StringLength(500)] 
    public string? RejectReason { get; set; }
}

public class ShiftRequestResponse
{
    public Guid RequestId { get; set; }
    public Guid UserId { get; set; }
    public string DoctorName { get; set; } = null!;
    public ShiftRequestType RequestType { get; set; }
    public DateOnly RequestDate { get; set; }
    public ShiftType ShiftType { get; set; }
    public string ShiftLabel { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public ShiftRequestStatus Status { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectReason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DayShiftSummary
{
    public DateOnly Date { get; set; }
    public ShiftInfo Morning { get; set; } = null!;
    public ShiftInfo Afternoon { get; set; } = null!;
    public ShiftInfo? Evening { get; set; }
}

public class ShiftInfo
{
    public string Status { get; set; } = null!; // "WORKING" | "OFF" | "HAS_BOOKINGS" | "PAST"
    public int TotalSlots { get; set; }
    public int BookedSlots { get; set; }
    public int ClosedSlots { get; set; }
    public ShiftRequestType? PendingRequestType { get; set; }
}
