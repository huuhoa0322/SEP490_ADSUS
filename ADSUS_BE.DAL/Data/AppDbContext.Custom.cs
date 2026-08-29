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

        // Module Kế toán (Billing)
        modelBuilder.HasPostgresEnum<InvoiceStatus>("public", "invoice_status");
        modelBuilder.HasPostgresEnum<PaymentMethod>("public", "payment_method");

        // Hai enum của module khác, khai ở đây vì Dashboard (UC-05) cần đếm theo trạng thái.
        // Xem chú thích trong Enums.cs. Ai làm Module 5 / Module 8 dùng lại, đừng khai lại.
        modelBuilder.HasPostgresEnum<AppointmentStatus>("public", "appointment_status");

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

        // ---------- Module 4: Medical Record ----------
        // Ba enum này đã được khai bằng chuỗi trong AppDbContext.cs (bản scaffold), nhưng bản
        // đó không gắn với kiểu CLR nào. Khai lại theo kiểu để EF biết PatientProfile.Gender,
        // Case.Status, Prescription.Status ánh xạ sang enum nào.
        modelBuilder.HasPostgresEnum<GenderType>("public", "gender_type");
        modelBuilder.HasPostgresEnum<CaseStatus>("public", "case_status");
        modelBuilder.HasPostgresEnum<PrescriptionStatus>("public", "prescription_status");

        modelBuilder.Entity<PatientProfile>(entity =>
        {
            entity.Property(e => e.Gender)
                .HasColumnName("gender")
                .HasDefaultValue(GenderType.Female);
        });

        modelBuilder.Entity<Case>(entity =>
        {
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasDefaultValue(CaseStatus.Created);
        });

        // Cột của Module 7 nhưng phải map ở đây: CaseResponse nhúng trạng thái đơn thuốc (#23).
        // Ai làm Module 7 dùng lại, đừng khai lại.
        modelBuilder.Entity<Prescription>(entity =>
        {
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasDefaultValue(PrescriptionStatus.Active);
        });

        // ---------- Module 7: Medication Intake ----------
        modelBuilder.HasPostgresEnum<IntakeStatus>("public", "intake_status");
        modelBuilder.HasPostgresEnum<ReminderSlot>("public", "reminder_slot");

        modelBuilder.Entity<MedicationIntakeLog>(entity =>
        {
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasDefaultValue(IntakeStatus.Pending);
        });

        modelBuilder.Entity<PrescriptionItem>(entity =>
        {
            entity.Property(e => e.ScheduleSlots)
                .HasColumnName("schedule_slots")
                .HasColumnType("reminder_slot[]");
        });
    }
}
