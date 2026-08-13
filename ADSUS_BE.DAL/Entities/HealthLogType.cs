namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Loại nhật ký sức khỏe: EXERCISE (tập thể dục) hoặc DIET (ăn uống).
/// </summary>
public enum HealthLogType
{
    /// <summary>
    /// Nhật ký tập thể dục
    /// </summary>
    EXERCISE = 0,

    /// <summary>
    /// Nhật ký ăn uống
    /// </summary>
    DIET = 1
}
