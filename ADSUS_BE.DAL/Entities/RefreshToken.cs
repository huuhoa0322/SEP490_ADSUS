using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Stores refresh tokens for JWT token renewal (SignalR support)
/// </summary>
public partial class RefreshToken
{
    /// <summary>
    /// Primary key
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// FK to users table
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// SHA256 hash of the refresh token (never store plain text)
    /// </summary>
    public string TokenHash { get; set; } = null!;

    /// <summary>
    /// When this refresh token expires (typically 7 days)
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// When this refresh token was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this token was revoked (NULL if still active)
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Optional: browser/device info for audit
    /// </summary>
    public string? DeviceInfo { get; set; }

    public virtual User User { get; set; } = null!;
}
