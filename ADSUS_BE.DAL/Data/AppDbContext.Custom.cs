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

        // Hai enum của module khác, khai ở đây vì Dashboard (UC-05) cần đếm theo trạng thái.
        // Xem chú thích trong Enums.cs. Ai làm Module 5 / Module 8 dùng lại, đừng khai lại.
        modelBuilder.HasPostgresEnum<AiResultStatus>("public", "ai_result_status");
        modelBuilder.HasPostgresEnum<AppointmentStatus>("public", "appointment_status");

        modelBuilder.Entity<AiResult>(entity =>
        {
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasDefaultValue(AiResultStatus.PendingReview);
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasDefaultValue(AppointmentStatus.Booked);
        });

        modelBuilder.HasPostgresEnum<ModelVersionStatus>("public", "model_version_status");

        modelBuilder.Entity<AiModelVersion>(entity =>
        {
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasDefaultValue(ModelVersionStatus.Inactive);
        });
    }
}
