using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Danh mục dịch vụ phòng khám do Admin cấu hình giá. Admin có thể CRUD tự do.
/// </summary>
public partial class ClinicService
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<CaseClinicService> CaseClinicServices { get; set; } = new List<CaseClinicService>();
}
