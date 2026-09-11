using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Dịch vụ đã áp dụng cho ca khám. Hệ thống tự thêm hoặc doctor thêm tay. price_at_time = snapshot giá.
/// </summary>
public partial class CaseClinicService
{
    public Guid Id { get; set; }

    public Guid CaseId { get; set; }

    public Guid ClinicServiceId { get; set; }

    public decimal PriceAtTime { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Case Case { get; set; } = null!;

    public virtual ClinicService ClinicService { get; set; } = null!;
}
