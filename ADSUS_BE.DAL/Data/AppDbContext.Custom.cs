using ADSUS_BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Data;

// AppDbContext.cs được sinh tự động và BỊ GHI ĐÈ mỗi lần chạy scaffold --force.
// Mọi cấu hình viết tay phải đặt ở đây — scaffold gọi OnModelCreatingPartial ở cuối
// OnModelCreating nên phần này luôn được áp dụng và không bao giờ bị mất.
public partial class AppDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // Khai báo 2 enum PostgreSQL với EF Core.
        modelBuilder.HasPostgresEnum<UserRole>("public", "user_role");
        modelBuilder.HasPostgresEnum<UserStatus>("public", "user_status");

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.Role)
                .HasColumnName("role");

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasDefaultValue(UserStatus.Active);
        });
    }
}
