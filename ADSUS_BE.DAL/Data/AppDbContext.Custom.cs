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

            // Gender - chuyển từ patient_profiles (2026-01)
            entity.Property(e => e.Gender)
                .HasColumnName("gender");
        });

        // Module Kế toán (Billing)
        modelBuilder.HasPostgresEnum<InvoiceStatus>("public", "invoice_status");
        modelBuilder.HasPostgresEnum<PaymentMethod>("public", "payment_method");
        modelBuilder.HasPostgresEnum<InvoiceItemType>("public", "invoice_item_type");

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

        // Gender đã chuyển sang User entity (2026-01) - PatientProfile không còn cột gender

        modelBuilder.Entity<Case>(entity =>
        {
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .ValueGeneratedNever();
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

        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.Property(e => e.ItemType).HasColumnName("item_type");
        });

        modelBuilder.Entity<ServiceFeedback>(entity =>
        {
            entity.HasOne(d => d.Case)
                .WithOne(p => p.ServiceFeedback)
                .HasForeignKey<ServiceFeedback>(d => d.CaseId)
                .IsRequired(false);
        });

        // ---------- Module: Shift Request ----------
        modelBuilder.HasPostgresEnum<ShiftRequestType>("public", "shift_request_type");
        modelBuilder.HasPostgresEnum<ShiftType>("public", "shift_type");
        modelBuilder.HasPostgresEnum<ShiftRequestStatus>("public", "shift_request_status");

        modelBuilder.Entity<ShiftRequest>(entity =>
        {
            entity.Property(e => e.RequestType)
                .HasColumnName("request_type");

            entity.Property(e => e.ShiftType)
                .HasColumnName("shift_type");

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasDefaultValue(ShiftRequestStatus.Pending);
        });

        // ---------- Module: Patient Relationship (Đặt lịch cho người thân) ----------
        // PatientRelationship DbSet — nằm đây thay vì AppDbContext.cs vì scaffold ghi đè.

        modelBuilder.Entity<PatientRelationship>(entity =>
        {
            entity.HasKey(e => e.RelationshipId).HasName("pk_patient_relationships");

            entity.ToTable("patient_relationships");

            entity.HasIndex(e => e.UserId, "idx_patient_relationships_user");

            entity.HasIndex(e => e.PatientProfileId, "idx_patient_relationships_patient");

            entity.HasIndex(e => new { e.UserId, e.PatientProfileId }, "uq_user_patient_relationship")
                .IsUnique();

            entity.Property(e => e.RelationshipId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("relationship_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.PatientProfileId).HasColumnName("patient_profile_id");
            entity.Property(e => e.RelationshipName)
                .HasMaxLength(50)
                .HasColumnName("relationship_name");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User)
                .WithMany(p => p.PatientRelationships)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_patient_relationships_user");

            entity.HasOne(d => d.PatientProfile)
                .WithMany(p => p.PatientRelationships)
                .HasForeignKey(d => d.PatientProfileId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_patient_relationships_profile");
        });

        // PatientProfile guest columns
        modelBuilder.Entity<PatientProfile>(entity =>
        {
            // Guest profile columns đã được khai báo trong PatientProfile.Custom.cs
            // ở mức partial class với [Column] attribute
        });

        // Appointment: booked_by_user_id cho đặt hộ
        modelBuilder.Entity<Appointment>(entity =>
        {
            // Bổ sung navigation property cho Appointment entity
            entity.HasOne(d => d.BookedByUser)
                .WithMany(p => p.Appointments)
                .HasForeignKey(d => d.BookedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_appointments_booked_by");

            // PatientRelationship navigation và foreign key
            entity.HasOne(d => d.PatientRelationship)
                .WithMany(p => p.Appointments)
                .HasForeignKey(d => d.RelationshipId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_appointments_relationship");
        });
    }
}
