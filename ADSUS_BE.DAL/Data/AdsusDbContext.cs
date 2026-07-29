using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Data;

/// <summary>
/// Skeleton placeholder — kept for forward compatibility (xem §13 SUPABASE_SCAFFOLD_GUIDE.md).
/// Module 7 dùng <see cref="AppDbContext"/> master làm DbContext chính thức (17 DbSets, 687 dòng
/// OnModelCreating khai báo FK / enums / indexes / snake_case columns) — KHÔNG thêm DbSet vào
/// class này. File để trống để AppDbContext là single source of truth cho cả team.
/// </summary>
public class AdsusDbContext : DbContext
{
    public AdsusDbContext(DbContextOptions<AdsusDbContext> options) : base(options)
    {
    }
}
