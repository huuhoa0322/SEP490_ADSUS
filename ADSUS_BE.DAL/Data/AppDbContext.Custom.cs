using ADSUS_BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Data;

// AppDbContext.cs is generated and gets OVERWRITTEN by every `scaffold --force` run.
// Hand-written configuration must live here instead: the generated OnModelCreating calls
// OnModelCreatingPartial at the end, so this always applies and is never lost.
public partial class AppDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // Declare the two PostgreSQL enums to EF Core.
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
