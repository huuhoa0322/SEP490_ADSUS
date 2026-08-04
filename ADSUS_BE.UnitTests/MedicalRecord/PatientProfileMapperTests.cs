using ADSUS_BE.BLL.MedicalRecord.Mappers;

namespace ADSUS_BE.UnitTests.MedicalRecord;

public class PatientProfileMapperTests
{
    [Fact]
    public void ToResponse_MapsUserFieldsAsReadOnly()
    {
        // Arrange
        var user = MedicalRecordTestData.MakePatientUser("Nguyễn Thị Hoa");
        var profile = MedicalRecordTestData.MakePatientProfile(user);

        // Act
        var response = PatientProfileMapper.ToResponse(profile);

        // Assert
        Assert.Equal(profile.PatientProfileId, response.PatientProfileId);
        Assert.Equal(user.UserId, response.PatientUserId);
        Assert.Equal("Nguyễn Thị Hoa", response.FullName);
        Assert.Equal(user.Phone, response.Phone);
        Assert.Equal(user.DateOfBirth, response.DateOfBirth);
    }

    [Fact]
    public void ToResponse_GenderSerializesAsUppercaseApiString()
    {
        // Arrange
        var profile = MedicalRecordTestData.MakePatientProfile();
        profile.Gender = ADSUS_BE.DAL.Entities.GenderType.Other;

        // Act
        var response = PatientProfileMapper.ToResponse(profile);

        // Assert
        Assert.Equal("OTHER", response.Gender);
    }
}
