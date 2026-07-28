using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

public partial class AuditLog
{
    public Guid LogId { get; set; }

    public Guid ActorId { get; set; }

    public string Action { get; set; } = null!;

    public string? Detail { get; set; }

    public DateTime PerformedAt { get; set; }

    public virtual User Actor { get; set; } = null!;
}
