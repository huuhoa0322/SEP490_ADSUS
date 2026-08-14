using ADSUS_BE.BLL.MedicalRecord.Mappers;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.UnitTests.MedicalRecord;

public class CaseMapperTests
{
    [Fact]
    public void ToStaffResponse_IncludesClinicalInfoAndPatientProfile()
    {
        // Arrange — bản Bác sĩ/Điều dưỡng PHẢI có đủ mọi trường nội bộ.
        var medicalCase = MedicalRecordTestData.MakeCase(status: CaseStatus.Confirmed);

        // Act
        var response = CaseMapper.ToStaffResponse(medicalCase, imageUrls: new Dictionary<Guid, string?>());

        // Assert
        Assert.Equal(medicalCase.ClinicalInfo, response.ClinicalInfo);
        Assert.NotNull(response.PatientProfile);
        Assert.Equal(medicalCase.PatientProfileId, response.PatientProfileId);
        Assert.Equal(medicalCase.CreatedAt, response.CreatedAt);
    }

    [Fact]
    public void ToPatientResponse_TypeHasNoClinicalOrInternalFields()
    {
        // Arrange — kiểm bằng phản chiếu (reflection): PatientCaseResponse không được có bất
        // kỳ property nào trong nhóm dữ liệu chỉ dành cho Bác sĩ/Điều dưỡng (GB-05). Đây là
        // test khoá lại tính chất "2 kiểu tách biệt", không phải test giá trị cụ thể.
        var forbiddenPropertyNames = new[]
        {
            "ClinicalInfo", "PatientProfileId", "PatientProfile",
            "UltrasoundImages", "AiResults", "CreatedAt", "UpdatedAt",
        };

        // Act
        var actualPropertyNames = typeof(ADSUS_BE.BLL.MedicalRecord.DTOs.PatientCaseResponse)
            .GetProperties()
            .Select(p => p.Name)
            .ToHashSet();

        // Assert
        foreach (var forbidden in forbiddenPropertyNames)
        {
            Assert.DoesNotContain(forbidden, actualPropertyNames);
        }
    }

    [Fact]
    public void ToPatientResponse_MapsOnlyPatientVisibleFields()
    {
        // Arrange
        var medicalCase = MedicalRecordTestData.MakeCase(status: CaseStatus.Confirmed);

        // Act
        var response = CaseMapper.ToPatientResponse(medicalCase);

        // Assert
        Assert.Equal(medicalCase.CaseId, response.CaseId);
        Assert.Equal(medicalCase.FinalDiagnosis, response.FinalDiagnosis);
        Assert.Equal(medicalCase.DoctorConclusion, response.DoctorConclusion);
        Assert.Equal("CONFIRMED", response.Status);
    }

    [Fact]
    public void ToImageResponse_NullSignedUrl_MapsToNullImageUrlWithoutThrowing()
    {
        // Arrange — dữ liệu seed có file_ref trỏ vào object không tồn tại, ký thất bại phải
        // ra null chứ không được văng exception.
        var image = new UltrasoundImage
        {
            ImageId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            FileRef = "khong-ton-tai/anh.png",
            UploadedAt = DateTime.UtcNow,
            Note = null,
        };

        // Act
        var response = CaseMapper.ToImageResponse(image, signedUrl: null);

        // Assert
        Assert.Null(response.ImageUrl);
        Assert.Equal(image.ImageId, response.ImageId);
    }

    [Fact]
    public void SelectLatestPrescription_SamePrescribedDate_BreaksTieByCreatedAtDescending()
    {
        // Arrange — đây là test khoá lại chính bug đã fix: 2 đơn cùng ngày kê, đơn tạo SAU
        // (CreatedAt lớn hơn) phải luôn được chọn làm "hiện hành", cho cả #23 lẫn #27 (PDF).
        var medicalCase = MedicalRecordTestData.MakeCase(status: CaseStatus.Confirmed);
        var sameDay = new DateOnly(2026, 7, 30);

        var older = MedicalRecordTestData.MakePrescription(
            medicalCase, sameDay, createdAt: new DateTime(2026, 7, 30, 8, 0, 0, DateTimeKind.Utc));
        var newer = MedicalRecordTestData.MakePrescription(
            medicalCase, sameDay, createdAt: new DateTime(2026, 7, 30, 14, 0, 0, DateTimeKind.Utc));

        // Act
        var selected = CaseMapper.SelectLatestPrescription(medicalCase);

        // Assert
        Assert.Equal(newer.PrescriptionId, selected!.PrescriptionId);
        Assert.NotEqual(older.PrescriptionId, selected.PrescriptionId);
    }

    [Fact]
    public void SelectLatestPrescription_DifferentDates_PicksMostRecentPrescribedDate()
    {
        // Arrange
        var medicalCase = MedicalRecordTestData.MakeCase(status: CaseStatus.Confirmed);

        var earlier = MedicalRecordTestData.MakePrescription(
            medicalCase, new DateOnly(2026, 7, 10), createdAt: DateTime.UtcNow.AddDays(-20));
        var later = MedicalRecordTestData.MakePrescription(
            medicalCase, new DateOnly(2026, 7, 25), createdAt: DateTime.UtcNow.AddDays(-5));

        // Act
        var selected = CaseMapper.SelectLatestPrescription(medicalCase);

        // Assert
        Assert.Equal(later.PrescriptionId, selected!.PrescriptionId);
        Assert.NotEqual(earlier.PrescriptionId, selected.PrescriptionId);
    }

    [Fact]
    public void SelectLatestPrescription_NoPrescriptions_ReturnsNull()
    {
        // Arrange
        var medicalCase = MedicalRecordTestData.MakeCase();

        // Act
        var selected = CaseMapper.SelectLatestPrescription(medicalCase);

        // Assert
        Assert.Null(selected);
    }

    [Fact]
    public void ToSummary_MapsCoreFieldsOnly()
    {
        // Arrange
        var medicalCase = MedicalRecordTestData.MakeCase(status: CaseStatus.End);

        // Act
        var summary = CaseMapper.ToSummary(medicalCase);

        // Assert
        Assert.Equal(medicalCase.CaseId, summary.CaseId);
        Assert.Equal(medicalCase.VisitDate, summary.VisitDate);
        Assert.Equal("END", summary.Status);
        Assert.Equal(medicalCase.DoctorId, summary.DoctorId);
    }
}
