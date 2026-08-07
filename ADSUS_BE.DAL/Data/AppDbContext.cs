using System;
using System.Collections.Generic;
using ADSUS_BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AiChatMessage> AiChatMessages { get; set; }

    public virtual DbSet<AiModelVersion> AiModelVersions { get; set; }

    public virtual DbSet<AiPrediction> AiPredictions { get; set; }

    public virtual DbSet<Appointment> Appointments { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<BlogPost> BlogPosts { get; set; }

    public virtual DbSet<Case> Cases { get; set; }

    public virtual DbSet<DoctorAnnotation> DoctorAnnotations { get; set; }

    public virtual DbSet<HealthLog> HealthLogs { get; set; }

    public virtual DbSet<MedicationIntakeLog> MedicationIntakeLogs { get; set; }

    public virtual DbSet<Medicine> Medicines { get; set; }

    public virtual DbSet<PatientProfile> PatientProfiles { get; set; }

    public virtual DbSet<PatientReminderPreference> PatientReminderPreferences { get; set; }

    public virtual DbSet<Prescription> Prescriptions { get; set; }

    public virtual DbSet<PrescriptionItem> PrescriptionItems { get; set; }

    public virtual DbSet<ScheduleSlot> ScheduleSlots { get; set; }

    public virtual DbSet<ServiceFeedback> ServiceFeedbacks { get; set; }

    public virtual DbSet<UltrasoundImage> UltrasoundImages { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("ai_result_status", new[] { "PENDING_REVIEW", "CONFIRMED", "REJECTED" })
            .HasPostgresEnum("appointment_status", new[] { "BOOKED", "CANCELLED" })
            .HasPostgresEnum("auth", "aal_level", new[] { "aal1", "aal2", "aal3" })
            .HasPostgresEnum("auth", "code_challenge_method", new[] { "s256", "plain" })
            .HasPostgresEnum("auth", "factor_status", new[] { "unverified", "verified" })
            .HasPostgresEnum("auth", "factor_type", new[] { "totp", "webauthn", "phone" })
            .HasPostgresEnum("auth", "oauth_authorization_status", new[] { "pending", "approved", "denied", "expired" })
            .HasPostgresEnum("auth", "oauth_client_type", new[] { "public", "confidential" })
            .HasPostgresEnum("auth", "oauth_registration_type", new[] { "dynamic", "manual" })
            .HasPostgresEnum("auth", "oauth_response_type", new[] { "code" })
            .HasPostgresEnum("auth", "one_time_token_type", new[] { "confirmation_token", "reauthentication_token", "recovery_token", "email_change_token_new", "email_change_token_current", "phone_change_token" })
            .HasPostgresEnum("blog_status", new[] { "DRAFT", "PUBLISHED" })
            .HasPostgresEnum("case_status", new[] { "CREATED", "ANALYZED", "CONFIRMED" })
            .HasPostgresEnum("chat_role", new[] { "USER", "ASSISTANT" })
            .HasPostgresEnum("gender_type", new[] { "FEMALE", "MALE", "OTHER" })
            .HasPostgresEnum("health_log_type", new[] { "EXERCISE", "DIET" })
            .HasPostgresEnum("intake_status", new[] { "PENDING", "TAKEN" })
            .HasPostgresEnum("model_version_status", new[] { "ACTIVE", "INACTIVE" })
            .HasPostgresEnum("prescription_status", new[] { "ACTIVE", "COMPLETED" })
            .HasPostgresEnum("realtime", "action", new[] { "INSERT", "UPDATE", "DELETE", "TRUNCATE", "ERROR" })
            .HasPostgresEnum("realtime", "equality_op", new[] { "eq", "neq", "lt", "lte", "gt", "gte", "in", "like", "ilike", "is", "match", "imatch", "isdistinct" })
            .HasPostgresEnum("reminder_slot", new[] { "MORNING", "NOON", "EVENING" })
            .HasPostgresEnum("slot_status", new[] { "OPEN", "CLOSED" })
            .HasPostgresEnum("storage", "buckettype", new[] { "STANDARD", "ANALYTICS", "VECTOR" })
            .HasPostgresEnum("user_role", new[] { "ADMIN", "DOCTOR", "PATIENT", "NURSE" })
            .HasPostgresEnum("user_status", new[] { "ACTIVE", "LOCKED", "DEACTIVATED" })
            .HasPostgresExtension("extensions", "pg_stat_statements")
            .HasPostgresExtension("extensions", "pgcrypto")
            .HasPostgresExtension("extensions", "uuid-ossp")
            .HasPostgresExtension("btree_gist")
            .HasPostgresExtension("vault", "supabase_vault");

        modelBuilder.Entity<AiChatMessage>(entity =>
        {
            entity.HasKey(e => e.MessageId).HasName("pk_ai_chat_messages");

            entity.ToTable("ai_chat_messages", tb => tb.HasComment("Lịch sử hội thoại chatbot (FT-39) — lưu để bệnh nhân xem lại, thay cho quyết định trước đó là không lưu. role phân biệt lượt hỏi (USER) và lượt trả lời (ASSISTANT). Chỉ chủ tài khoản truy cập được lịch sử của mình (§3.2 Restricted)."));

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "idx_ai_chat_messages_user_timeline");

            entity.Property(e => e.MessageId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("message_id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.AiChatMessages)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_ai_chat_messages_user");
        });

        modelBuilder.Entity<AiModelVersion>(entity =>
        {
            entity.HasKey(e => e.ModelVersionId).HasName("pk_ai_model_versions");

            entity.ToTable("ai_model_versions", tb => tb.HasComment("Phiên bản mô hình AI (thêm mới / kích hoạt / rollback / theo dõi — FT-23/24/25). Chỉ 2 trạng thái Active/Inactive — phiên bản mới thêm mặc định Inactive cho tới khi Admin kích hoạt. Partial unique index bảo đảm chỉ 1 phiên bản ACTIVE. Rollback = ACTIVE → INACTIVE và kích hoạt bản khác."));

            entity.HasIndex(e => e.VersionCode, "uq_ai_model_versions_code").IsUnique();

            entity.Property(e => e.ModelVersionId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("model_version_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.HfFilename)
                .HasMaxLength(255)
                .HasColumnName("hf_filename");
            entity.Property(e => e.HfRepoId)
                .HasMaxLength(255)
                .HasColumnName("hf_repo_id");
            entity.Property(e => e.MetricsMap50)
                .HasPrecision(5, 2)
                .HasComment("Đơn vị %. Ngưỡng KPI: > 85%.")
                .HasColumnName("metrics_map50");
            entity.Property(e => e.MetricsPrecision)
                .HasPrecision(5, 2)
                .HasComment("Chỉ số đo offline khi đăng ký, đơn vị %. Ngưỡng KPI nghiên cứu: > 90%.")
                .HasColumnName("metrics_precision");
            entity.Property(e => e.MetricsRecall)
                .HasPrecision(4, 3)
                .HasComment("Thang 0–1. Ngưỡng KPI: > 0.90.")
                .HasColumnName("metrics_recall");
            entity.Property(e => e.RegisteredAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("registered_at");
            entity.Property(e => e.RegisteredBy).HasColumnName("registered_by");
            entity.Property(e => e.VersionCode)
                .HasMaxLength(50)
                .HasColumnName("version_code");

            entity.HasOne(d => d.RegisteredByNavigation).WithMany(p => p.AiModelVersions)
                .HasForeignKey(d => d.RegisteredBy)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_ai_model_versions_registered_by");
        });

        modelBuilder.Entity<AiPrediction>(entity =>
        {
            entity.HasKey(e => e.PredictionId).HasName("ai_predictions_pkey");

            entity.ToTable("ai_predictions");

            entity.HasIndex(e => new { e.CaseId, e.ImageId }, "idx_ai_preds_case_image");

            entity.HasIndex(e => e.Confidence, "idx_ai_preds_confidence").IsDescending();

            entity.Property(e => e.PredictionId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("prediction_id");
            entity.Property(e => e.BboxXmax).HasColumnName("bbox_xmax");
            entity.Property(e => e.BboxXmin).HasColumnName("bbox_xmin");
            entity.Property(e => e.BboxYmax).HasColumnName("bbox_ymax");
            entity.Property(e => e.BboxYmin).HasColumnName("bbox_ymin");
            entity.Property(e => e.CaseId).HasColumnName("case_id");
            entity.Property(e => e.Confidence).HasColumnName("confidence");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ImageId).HasColumnName("image_id");
            entity.Property(e => e.ModelVersionId).HasColumnName("model_version_id");

            entity.HasOne(d => d.Case).WithMany(p => p.AiPredictions)
                .HasForeignKey(d => d.CaseId)
                .HasConstraintName("fk_ai_preds_case");

            entity.HasOne(d => d.Image).WithMany(p => p.AiPredictions)
                .HasForeignKey(d => d.ImageId)
                .HasConstraintName("fk_ai_preds_image");

            entity.HasOne(d => d.ModelVersion).WithMany(p => p.AiPredictions)
                .HasForeignKey(d => d.ModelVersionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_ai_preds_model");
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(e => e.AppointmentId).HasName("pk_appointments");

            entity.ToTable("appointments", tb => tb.HasComment("Lịch khám đã đặt (UC-13/14). Đổi lịch = CANCELLED dòng cũ + tạo dòng mới (giữ vết). Job JOB-02 đọc bảng này để nhắc lịch qua push. Chỉ 2 trạng thái BOOKED/CANCELLED — không có COMPLETED: lịch \"đã qua\" suy ra ở tầng ứng dụng bằng cách so schedule_slots.end_time với NOW(), không lưu trạng thái riêng (tránh job quét/cập nhật hàng loạt)."));

            entity.HasIndex(e => new { e.PatientProfileId, e.CreatedAt }, "idx_appointments_patient").IsDescending(false, true);

            entity.HasIndex(e => e.SlotId, "idx_appointments_slot");

            entity.HasIndex(e => new { e.SlotId, e.PatientProfileId }, "uq_appointments_active_booking")
                .IsUnique()
                .HasFilter("(status = 'BOOKED'::appointment_status)");

            entity.Property(e => e.AppointmentId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("appointment_id");
            entity.Property(e => e.CalendarSyncedAt)
                .HasComment("Mốc đã đẩy sự kiện sang Calendar thiết bị (FT-34, one-way sync) — sự kiện nằm NGOÀI hệ thống, chỉ giữ timestamp.")
                .HasColumnName("calendar_synced_at");
            entity.Property(e => e.CancelledReason).HasColumnName("cancelled_reason");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.PatientProfileId).HasColumnName("patient_profile_id");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.SlotId).HasColumnName("slot_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.PatientProfile).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.PatientProfileId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_appointments_patient");

            entity.HasOne(d => d.Slot).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.SlotId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_appointments_slot");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("pk_audit_logs");

            entity.ToTable("audit_logs");

            entity.Property(e => e.LogId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("log_id");
            entity.Property(e => e.Action)
                .HasColumnType("character varying")
                .HasColumnName("action");
            entity.Property(e => e.ActorId).HasColumnName("actor_id");
            entity.Property(e => e.Detail).HasColumnName("detail");
            entity.Property(e => e.PerformedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("performed_at");

            entity.HasOne(d => d.Actor).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.ActorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_audit_logs_actor");
        });

        modelBuilder.Entity<BlogPost>(entity =>
        {
            entity.HasKey(e => e.PostId).HasName("pk_blog_posts");

            entity.ToTable("blog_posts", tb => tb.HasComment("Blog sức khỏe (UC-23/24). Bệnh nhân chỉ thấy PUBLISHED (§3.2: Patient chỉ có quyền View)."));

            entity.HasIndex(e => e.PublishedAt, "idx_blog_posts_published")
                .IsDescending()
                .HasFilter("(status = 'PUBLISHED'::blog_status)");

            entity.Property(e => e.PostId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("post_id");
            entity.Property(e => e.AuthorId).HasColumnName("author_id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.PublishedAt).HasColumnName("published_at");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Author).WithMany(p => p.BlogPosts)
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_blog_posts_author");
        });

        modelBuilder.Entity<Case>(entity =>
        {
            entity.HasKey(e => e.CaseId).HasName("pk_cases");

            entity.ToTable("cases", tb => tb.HasComment("Một lượt khám của một bệnh nhân — mốc neo cho ảnh siêu âm, kết quả AI, đơn thuốc. Theo dõi tiến triển (FT-22) = so sánh dữ liệu qua nhiều cases theo visit_date. Vòng đời CREATED → ANALYZED → CONFIRMED một chiều (GBR) — enforce ở tầng ứng dụng."));

            entity.HasIndex(e => new { e.DoctorId, e.VisitDate }, "idx_cases_doctor_worklist").IsDescending(false, true);

            entity.HasIndex(e => new { e.PatientProfileId, e.VisitDate }, "idx_cases_patient_timeline").IsDescending(false, true);

            entity.Property(e => e.CaseId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("case_id");
            entity.Property(e => e.ClinicalInfo)
                .HasComment("Thông tin lâm sàng bác sĩ nhập khi tạo ca (FT-14) — đầu vào phụ trợ cho AI.")
                .HasColumnName("clinical_info");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DoctorConclusion).HasColumnName("doctor_conclusion");
            entity.Property(e => e.DoctorId).HasColumnName("doctor_id");
            entity.Property(e => e.FinalDiagnosis)
                .HasComment("Kết luận chẩn đoán cuối của bác sĩ SAU khi duyệt kết quả AI — mỗi ca đúng 1 kết luận (attribute, không tách entity).")
                .HasColumnName("final_diagnosis");
            entity.Property(e => e.PatientProfileId).HasColumnName("patient_profile_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.VisitDate)
                .HasDefaultValueSql("CURRENT_DATE")
                .HasColumnName("visit_date");

            entity.HasOne(d => d.Doctor).WithMany(p => p.Cases)
                .HasForeignKey(d => d.DoctorId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_cases_doctor");

            entity.HasOne(d => d.PatientProfile).WithMany(p => p.Cases)
                .HasForeignKey(d => d.PatientProfileId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_cases_patient_profile");
        });

        modelBuilder.Entity<DoctorAnnotation>(entity =>
        {
            entity.HasKey(e => e.AnnotationId).HasName("doctor_annotations_pkey");

            entity.ToTable("doctor_annotations");

            entity.HasIndex(e => new { e.CaseId, e.ImageId }, "idx_doc_annots_case_image");

            entity.Property(e => e.AnnotationId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("annotation_id");
            entity.Property(e => e.BboxXmax).HasColumnName("bbox_xmax");
            entity.Property(e => e.BboxXmin).HasColumnName("bbox_xmin");
            entity.Property(e => e.BboxYmax).HasColumnName("bbox_ymax");
            entity.Property(e => e.BboxYmin).HasColumnName("bbox_ymin");
            entity.Property(e => e.CaseId).HasColumnName("case_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ImageId).HasColumnName("image_id");
            entity.Property(e => e.Source)
                .HasColumnType("character varying")
                .HasColumnName("source");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Case).WithMany(p => p.DoctorAnnotations)
                .HasForeignKey(d => d.CaseId)
                .HasConstraintName("fk_doc_annots_case");

            entity.HasOne(d => d.Image).WithMany(p => p.DoctorAnnotations)
                .HasForeignKey(d => d.ImageId)
                .HasConstraintName("fk_doc_annots_image");
        });

        modelBuilder.Entity<HealthLog>(entity =>
        {
            entity.HasKey(e => e.HealthLogId).HasName("pk_health_logs");

            entity.ToTable("health_logs", tb => tb.HasComment("Nhật ký FT-35. created_at là mốc JOB-03 kiểm tra chu kỳ nhắc 6 giờ (FT-40); widget màn hình chính (FT-41) đọc các dòng gần nhất."));

            entity.HasIndex(e => new { e.PatientProfileId, e.CreatedAt }, "idx_health_logs_patient_latest").IsDescending(false, true);

            entity.Property(e => e.HealthLogId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("health_log_id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.LogDate)
                .HasDefaultValueSql("CURRENT_DATE")
                .HasColumnName("log_date");
            entity.Property(e => e.PatientProfileId).HasColumnName("patient_profile_id");

            entity.HasOne(d => d.PatientProfile).WithMany(p => p.HealthLogs)
                .HasForeignKey(d => d.PatientProfileId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_health_logs_patient");
        });

        modelBuilder.Entity<MedicationIntakeLog>(entity =>
        {
            entity.HasKey(e => e.IntakeId).HasName("pk_medication_intake_logs");

            entity.ToTable("medication_intake_logs", tb => tb.HasComment("Mỗi liều thuốc = 1 dòng. Tuân thủ điều trị (FT-27) = tỉ lệ TAKEN trên tổng liều. PENDING là bổ sung vật lý: job sinh dòng trước, bệnh nhân xác nhận sau (FT-29). Không có trạng thái Missed/Skipped — JOB-01 nhắc lặp lại định kỳ khi còn PENDING, chỉ dừng khi bệnh nhân xác nhận TAKEN."));

            entity.HasIndex(e => e.ScheduledTime, "idx_medication_intake_logs_due").HasFilter("(status = 'PENDING'::intake_status)");

            entity.HasIndex(e => new { e.PrescriptionItemId, e.ScheduledTime }, "uq_medication_intake_logs_dose").IsUnique();

            entity.Property(e => e.IntakeId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("intake_id");
            entity.Property(e => e.ConfirmedAt).HasColumnName("confirmed_at");
            entity.Property(e => e.PrescriptionItemId).HasColumnName("prescription_item_id");
            entity.Property(e => e.ScheduledTime).HasColumnName("scheduled_time");

            entity.HasOne(d => d.PrescriptionItem).WithMany(p => p.MedicationIntakeLogs)
                .HasForeignKey(d => d.PrescriptionItemId)
                .HasConstraintName("fk_medication_intake_logs_item");
        });

        modelBuilder.Entity<Medicine>(entity =>
        {
            entity.HasKey(e => e.MedicineId).HasName("pk_medicines");

            entity.ToTable("medicines", tb => tb.HasComment("Danh mục thuốc dùng chung. Bác sĩ gõ tìm tên thuốc khi kê đơn (FT-30) qua ô tìm kiếm; nếu chưa có trong danh mục, hệ thống tự thêm mới để dùng lại cho lần sau — thay cho việc nhập tự do trước đây."));

            entity.Property(e => e.MedicineId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("medicine_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
        });

        modelBuilder.Entity<PatientProfile>(entity =>
        {
            entity.HasKey(e => e.PatientProfileId).HasName("pk_patient_profiles");

            entity.ToTable("patient_profiles", tb => tb.HasComment("Hồ sơ y tế nền của bệnh nhân (1–1 với users). Tách khỏi users để thực thi quy tắc lõi: Admin quản tài khoản nhưng KHÔNG truy cập dữ liệu y tế (§3.2) — ngoại lệ duy nhất là date_of_birth, đã chuyển lên users vì dùng chung cho cả 3 vai trò. user_id phải có role = PATIENT, created_by phải có role = DOCTOR — enforce ở tầng ứng dụng (FK không kiểm tra được role)."));

            entity.HasIndex(e => e.UserId, "uq_patient_profiles_user").IsUnique();

            entity.Property(e => e.PatientProfileId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("patient_profile_id");
            entity.Property(e => e.Allergies).HasColumnName("allergies");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasComment("Bác sĩ lập hồ sơ (UC-06). Bệnh nhân không tự đăng ký.")
                .HasColumnName("created_by");
            entity.Property(e => e.MedicalHistory).HasColumnName("medical_history");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PatientProfileCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_patient_profiles_created_by");

            entity.HasOne(d => d.User).WithOne(p => p.PatientProfileUser)
                .HasForeignKey<PatientProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_patient_profiles_user");
        });

        modelBuilder.Entity<PatientReminderPreference>(entity =>
        {
            entity.HasKey(e => e.PreferenceId).HasName("pk_patient_reminder_preferences");

            entity.ToTable("patient_reminder_preferences", tb => tb.HasComment("Giờ nhắc uống thuốc do bệnh nhân tự chỉnh theo từng khung (MORNING/NOON/EVENING), áp dụng cho MỌI thuốc — không gắn với 1 đơn cụ thể, chỉnh 1 lần dùng mãi về sau. Mặc định hệ thống khi bệnh nhân chưa có dòng tùy chỉnh: Sáng 07:00 / Trưa 12:00 / Tối 20:00 (áp ở tầng ứng dụng, không lưu dòng mặc định vào bảng này). JOB-01 tra bảng này khi sinh scheduled_time cho medication_intake_logs mới."));

            entity.Property(e => e.PreferenceId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("preference_id");
            entity.Property(e => e.CustomTime).HasColumnName("custom_time");
            entity.Property(e => e.PatientProfileId).HasColumnName("patient_profile_id");

            entity.HasOne(d => d.PatientProfile).WithMany(p => p.PatientReminderPreferences)
                .HasForeignKey(d => d.PatientProfileId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_patient_reminder_preferences_patient");
        });

        modelBuilder.Entity<Prescription>(entity =>
        {
            entity.HasKey(e => e.PrescriptionId).HasName("pk_prescriptions");

            entity.ToTable("prescriptions", tb => tb.HasComment("Đơn thuốc kê sau lượt khám (UC-18). Header — chi tiết thuốc nằm ở prescription_items."));

            entity.HasIndex(e => e.CaseId, "idx_prescriptions_case");

            entity.Property(e => e.PrescriptionId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("prescription_id");
            entity.Property(e => e.CaseId).HasColumnName("case_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DoctorId).HasColumnName("doctor_id");
            entity.Property(e => e.GeneralNote).HasColumnName("general_note");
            entity.Property(e => e.PrescribedDate)
                .HasDefaultValueSql("CURRENT_DATE")
                .HasColumnName("prescribed_date");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Case).WithMany(p => p.Prescriptions)
                .HasForeignKey(d => d.CaseId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_prescriptions_case");

            entity.HasOne(d => d.Doctor).WithMany(p => p.Prescriptions)
                .HasForeignKey(d => d.DoctorId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_prescriptions_doctor");
        });

        modelBuilder.Entity<PrescriptionItem>(entity =>
        {
            entity.HasKey(e => e.PrescriptionItemId).HasName("pk_prescription_items");

            entity.ToTable("prescription_items", tb => tb.HasComment("1 đơn chứa NHIỀU thuốc, mỗi thuốc liều/lịch riêng (repeating group → bảng riêng). Job nhắc thuốc (JOB-01) đọc schedule_slots + start_date + duration_days để sinh liều, tra thêm patient_reminder_preferences để lấy giờ cụ thể."));

            entity.HasIndex(e => e.MedicineId, "idx_prescription_items_medicine");

            entity.HasIndex(e => e.PrescriptionId, "idx_prescription_items_prescription");

            entity.Property(e => e.PrescriptionItemId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("prescription_item_id");
            entity.Property(e => e.Dosage)
                .HasMaxLength(100)
                .HasColumnName("dosage");
            entity.Property(e => e.DurationDays).HasColumnName("duration_days");
            entity.Property(e => e.Instructions).HasColumnName("instructions");
            entity.Property(e => e.MedicineId)
                .HasComment("Tra danh mục medicines qua ô tìm kiếm khi kê đơn; tự thêm mới vào danh mục nếu bác sĩ gõ tên chưa có (thay cho nhập tự do trước đây).")
                .HasColumnName("medicine_id");
            entity.Property(e => e.PrescriptionId).HasColumnName("prescription_id");
            entity.Property(e => e.StartDate)
                .HasDefaultValueSql("CURRENT_DATE")
                .HasColumnName("start_date");

            entity.HasOne(d => d.Medicine).WithMany(p => p.PrescriptionItems)
                .HasForeignKey(d => d.MedicineId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_prescription_items_medicine");

            entity.HasOne(d => d.Prescription).WithMany(p => p.PrescriptionItems)
                .HasForeignKey(d => d.PrescriptionId)
                .HasConstraintName("fk_prescription_items_prescription");
        });

        modelBuilder.Entity<ScheduleSlot>(entity =>
        {
            entity.HasKey(e => e.SlotId).HasName("pk_schedule_slots");

            entity.ToTable("schedule_slots", tb => tb.HasComment("Quỹ giờ khám bác sĩ công bố (UC-15) — không giới hạn số Appointment/slot, vòng đời chỉ Open → Closed (quyết định UCS 3.1, 23/07/2026)."));

            entity.HasIndex(e => new { e.SlotDate, e.DoctorId }, "idx_schedule_slots_open").HasFilter("(status = 'OPEN'::slot_status)");

            entity.HasIndex(e => new { e.DoctorId, e.SlotDate, e.StartTime }, "uq_schedule_slots_start").IsUnique();

            entity.Property(e => e.SlotId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("slot_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DoctorId).HasColumnName("doctor_id");
            entity.Property(e => e.EndTime).HasColumnName("end_time");
            entity.Property(e => e.SlotDate).HasColumnName("slot_date");
            entity.Property(e => e.StartTime).HasColumnName("start_time");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Doctor).WithMany(p => p.ScheduleSlots)
                .HasForeignKey(d => d.DoctorId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_schedule_slots_doctor");
        });

        modelBuilder.Entity<ServiceFeedback>(entity =>
        {
            entity.HasKey(e => e.FeedbackId).HasName("pk_service_feedbacks");

            entity.ToTable("service_feedbacks", tb => tb.HasComment("Phản hồi/đánh giá dịch vụ (FT-36), thang 1–5 sao."));

            entity.Property(e => e.FeedbackId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("feedback_id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.PatientProfileId).HasColumnName("patient_profile_id");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.SubmittedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("submitted_at");

            entity.HasOne(d => d.PatientProfile).WithMany(p => p.ServiceFeedbacks)
                .HasForeignKey(d => d.PatientProfileId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_service_feedbacks_patient");
        });

        modelBuilder.Entity<UltrasoundImage>(entity =>
        {
            entity.HasKey(e => e.ImageId).HasName("pk_ultrasound_images");

            entity.ToTable("ultrasound_images", tb => tb.HasComment("1 ca bệnh có NHIỀU ảnh (FT-13). File nhị phân nằm ngoài DB — file_ref là đường dẫn lưu trữ; ràng buộc dung lượng/định dạng kiểm ở tầng ứng dụng (TDS)."));

            entity.HasIndex(e => e.CaseId, "idx_ultrasound_images_case");

            entity.Property(e => e.ImageId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("image_id");
            entity.Property(e => e.CaseId).HasColumnName("case_id");
            entity.Property(e => e.FileRef)
                .HasMaxLength(500)
                .HasColumnName("file_ref");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("uploaded_at");

            entity.HasOne(d => d.Case).WithMany(p => p.UltrasoundImages)
                .HasForeignKey(d => d.CaseId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_ultrasound_images_case");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("pk_users");

            entity.ToTable("users", tb => tb.HasComment("Tài khoản đăng nhập cho cả 3 vai trò. Không bao giờ hard-delete — vô hiệu hóa bằng status = DEACTIVATED (data rule: accounts never permanently deleted)."));

            entity.HasIndex(e => e.Phone, "uq_users_phone").IsUnique();

            entity.Property(e => e.UserId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("user_id");
            entity.Property(e => e.BiometricEnabled)
                .HasDefaultValue(false)
                .HasComment("Cờ bật đăng nhập sinh trắc học (FT-03). Mẫu vân tay/khuôn mặt nằm trong secure enclave của OS — KHÔNG BAO GIỜ lưu trong DB.")
                .HasColumnName("biometric_enabled");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DateOfBirth)
                .HasComment("Chuyển lên từ patient_profiles — dùng chung cho cả 3 vai trò (trước chỉ Patient có). NULL cho phép vì Admin/Doctor không bắt buộc khai báo; với Patient đây là đầu vào lâm sàng phụ trợ cho AI (tuổi) — đúng tên đề tài \"…and Clinical Information\". DB không tách được quyền theo cột: tầng ứng dụng phải ẩn trường này khỏi mọi giao diện/API quản lý tài khoản mà Admin dùng khi role của tài khoản đó = PATIENT — giữ đúng tinh thần \"Admin không truy cập dữ liệu y tế\" (§2.3). Không ẩn với tài khoản ADMIN/DOCTOR.")
                .HasColumnName("date_of_birth");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasComment("Chỉ dùng để tự cấp lại mật khẩu khi quên (không dùng để đăng nhập). Hệ thống gửi mật khẩu mới qua email này khi người dùng yêu cầu.")
                .HasColumnName("email");
            entity.Property(e => e.FullName)
                .HasMaxLength(100)
                .HasColumnName("full_name");
            entity.Property(e => e.MustChangePassword)
                .HasDefaultValue(false)
                .HasComment("TRUE sau khi Admin cấp lại mật khẩu (FT-06) — hệ thống ép đổi mật khẩu ở lần đăng nhập kế tiếp (UC-25).")
                .HasColumnName("must_change_password");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.Phone)
                .HasMaxLength(15)
                .HasComment("Định danh đăng nhập duy nhất (thay cho username cũ) — Đăng nhập = số điện thoại + mật khẩu.")
                .HasColumnName("phone");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
