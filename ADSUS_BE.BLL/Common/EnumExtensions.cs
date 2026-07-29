using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.Common;

/// <summary>
/// Converts enums to the string form used in API responses and JWT claims.
/// C# members are PascalCase (Admin) while the database labels and the value clients expect
/// are uppercase (ADMIN). Keeping the conversion in one place means a change of convention
/// only has to be made here.
/// </summary>
public static class EnumExtensions
{
    public static string ToApiString(this UserRole role) => role.ToString().ToUpperInvariant();
}
