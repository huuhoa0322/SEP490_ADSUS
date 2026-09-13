using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Data.Configurations;

public class PatientRelationshipConfiguration : IEntityTypeConfiguration<PatientRelationship>
{
    public void Configure(EntityTypeBuilder<PatientRelationship> entity)
    {
        entity.ToTable("patient_relationships");

        entity.HasKey(e => e.RelationshipId);

        entity.Property(e => e.RelationshipId)
            .HasColumnName("relationship_id")
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        entity.Property(e => e.PatientProfileId)
            .HasColumnName("patient_profile_id")
            .IsRequired();

        entity.Property(e => e.RelationshipName)
            .HasColumnName("relationship_name")
            .HasMaxLength(50);

        entity.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        // Unique constraint: một user chỉ có một dòng cho mỗi patient profile
        entity.HasIndex(e => new { e.UserId, e.PatientProfileId })
            .IsUnique()
            .HasDatabaseName("uq_user_patient_relationship");

        // Foreign keys
        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.PatientProfile)
            .WithMany()
            .HasForeignKey(e => e.PatientProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
