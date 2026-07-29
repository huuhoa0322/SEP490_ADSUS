using NpgsqlTypes;

namespace ADSUS_BE.DAL.Entities;

// PostgreSQL enums cannot be scaffolded ("Enum column cannot be scaffolded"), so they are
// declared by hand here. [PgName] maps each member to the exact uppercase label stored in
// the database — without it Npgsql falls back to snake_case and the mapping breaks.

/// <summary>
/// Account role — the <c>user_role</c> enum in the database.
/// NURSE is defined in the UCS but not yet added to the database; it will follow later.
/// </summary>
public enum UserRole
{
    [PgName("ADMIN")] Admin,
    [PgName("DOCTOR")] Doctor,
    [PgName("PATIENT")] Patient,
}

/// <summary>
/// Account status — the <c>user_status</c> enum in the database.
/// Only Active accounts can sign in (UC-01 BR-01). Deactivated is terminal: it is never
/// reversed and the row is never hard-deleted.
/// </summary>
public enum UserStatus
{
    [PgName("ACTIVE")] Active,
    [PgName("LOCKED")] Locked,
    [PgName("DEACTIVATED")] Deactivated,
}
