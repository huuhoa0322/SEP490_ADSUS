using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.UnitTests.MedicalRecord;

/// <summary>
/// Dựng entity hợp lệ tối thiểu cho test Module 04 — tránh mỗi test class tự lặp lại việc
/// gán đủ navigation property (Doctor, PatientProfile.User, Prescriptions...) mà mapper cần.
/// </summary>
internal static class MedicalRecordTestData
{
    public static User MakeDoctor(string fullName = "BS. Lê Minh Hoàng") => new()
    {
        UserId = Guid.NewGuid(),
        FullName = fullName,
        Phone = "0913456789",
        PasswordHash = "khong-dung-toi-trong-test",
        Role = UserRole.Doctor,
        Status = UserStatus.Active,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    public static User MakeNurse(string fullName = "ĐD. Võ Thị Thu Hà") => new()
    {
        UserId = Guid.NewGuid(),
        FullName = fullName,
        Phone = "0915678901",
        PasswordHash = "khong-dung-toi-trong-test",
        Role = UserRole.Nurse,
        Status = UserStatus.Active,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    public static User MakePatientUser(string fullName = "Nguyễn Thị Hoa") => new()
    {
        UserId = Guid.NewGuid(),
        FullName = fullName,
        Phone = "0981111001",
        PasswordHash = "khong-dung-toi-trong-test",
        Role = UserRole.Patient,
        Status = UserStatus.Active,
        DateOfBirth = new DateOnly(1992, 5, 14),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    public static PatientProfile MakePatientProfile(User? user = null, Guid? createdBy = null)
    {
        var patientUser = user ?? MakePatientUser();

        return new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = patientUser.UserId,
            User = patientUser,
            Gender = GenderType.Female,
            CreatedBy = createdBy ?? Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public static Case MakeCase(
        PatientProfile? profile = null,
        User? doctor = null,
        CaseStatus status = CaseStatus.Created)
    {
        var patientProfile = profile ?? MakePatientProfile();
        var responsibleDoctor = doctor ?? MakeDoctor();

        return new Case
        {
            CaseId = Guid.NewGuid(),
            PatientProfileId = patientProfile.PatientProfileId,
            PatientProfile = patientProfile,
            DoctorId = responsibleDoctor.UserId,
            Doctor = responsibleDoctor,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ClinicalInfo = "Đau tức vú trái",
            Status = status,
            FinalDiagnosis = status == CaseStatus.Confirmed ? "U tuyến xơ vú phải (BI-RADS 3)" : null,
            DoctorConclusion = status == CaseStatus.Confirmed ? "Theo dõi định kỳ sau 6 tháng" : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>Gắn 1 đơn thuốc vào ca — dùng để test SelectLatestPrescription/PDF/CaseResponse.</summary>
    public static Prescription MakePrescription(
        Case medicalCase,
        DateOnly prescribedDate,
        DateTime createdAt,
        string generalNote = "Uống sau ăn")
    {
        var medicine = new Medicine
        {
            MedicineId = Guid.NewGuid(),
            Name = "Paracetamol 500mg",
            CreatedAt = DateTime.UtcNow,
        };

        var prescription = new Prescription
        {
            PrescriptionId = Guid.NewGuid(),
            CaseId = medicalCase.CaseId,
            Case = medicalCase,
            DoctorId = medicalCase.DoctorId,
            Doctor = medicalCase.Doctor,
            PrescribedDate = prescribedDate,
            GeneralNote = generalNote,
            Status = PrescriptionStatus.Active,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };

        prescription.PrescriptionItems.Add(new PrescriptionItem
        {
            PrescriptionItemId = Guid.NewGuid(),
            PrescriptionId = prescription.PrescriptionId,
            Prescription = prescription,
            MedicineId = medicine.MedicineId,
            Medicine = medicine,
            Dosage = "1 viên/lần, 2 lần/ngày",
            DurationDays = 5,
            StartDate = prescribedDate,
            Instructions = "Uống sau ăn",
        });

        medicalCase.Prescriptions.Add(prescription);
        return prescription;
    }
}
