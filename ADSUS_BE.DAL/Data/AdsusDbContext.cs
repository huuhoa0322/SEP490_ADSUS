using ADSUS_BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Data
{
    public class AdsusDbContext : DbContext
    {
        public AdsusDbContext(DbContextOptions<AdsusDbContext> options) : base(options)
        {
        }

        // Module 7 — Prescription adherence (UC-18/19/20). DbSet names mirror AppDbContext
        // master to keep a single source of truth for table mapping; OnModelCreating in
        // AppDbContext owns the actual column / enum / FK configuration.
        public virtual DbSet<Prescription> Prescriptions { get; set; } = null!;
        public virtual DbSet<PrescriptionItem> PrescriptionItems { get; set; } = null!;
        public virtual DbSet<MedicationIntakeLog> MedicationIntakeLogs { get; set; } = null!;
        public virtual DbSet<PatientReminderPreference> PatientReminderPreferences { get; set; } = null!;
        public virtual DbSet<Medicine> Medicines { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Master AppDbContext already declares all entity configurations (FK, enums,
            // indexes, snake_case columns). ApplyConfigurationsFromAssembly is a no-op
            // until IEntityTypeConfiguration classes are added — kept for forward
            // compatibility so individual modules can ship their own configurations later.
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AdsusDbContext).Assembly);
        }
    }
}