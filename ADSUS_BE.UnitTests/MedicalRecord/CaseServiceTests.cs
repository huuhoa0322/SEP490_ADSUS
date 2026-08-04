using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.MedicalRecord.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.ExternalServices;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace ADSUS_BE.UnitTests.MedicalRecord;

public class CaseServiceTests
{
    private readonly Mock<ICaseRepository> _cases = new();
    private readonly Mock<IUltrasoundImageRepository> _images = new();
    private readonly Mock<IPatientProfileRepository> _profiles = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IFileStorageService> _storage = new();
    private readonly CaseService _sut;

    public CaseServiceTests()
    {
        _sut = new CaseService(
            _cases.Object, _images.Object, _profiles.Object, _users.Object,
            _storage.Object, Mock.Of<ILogger<CaseService>>());
    }

    // ---------- GetForStaffAsync ----------

    [Fact]
    public async Task GetForStaffAsync_ExistingCase_ReturnsFullStaffResponse()
    {
        // Arrange
        var medicalCase = MedicalRecordTestData.MakeCase(status: CaseStatus.Confirmed);
        _cases.Setup(r => r.GetDetailAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);

        // Act
        var response = await _sut.GetForStaffAsync(medicalCase.CaseId);

        // Assert
        Assert.Equal(medicalCase.CaseId, response.CaseId);
        Assert.Equal(medicalCase.ClinicalInfo, response.ClinicalInfo);
    }

    [Fact]
    public async Task GetForStaffAsync_CaseNotFound_ThrowsResourceNotFoundException()
    {
        // Arrange
        _cases.Setup(r => r.GetDetailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((Case?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => _sut.GetForStaffAsync(Guid.NewGuid()));
    }

    // ---------- GetForPatientAsync (GB-05: 3 kịch bản trượt phải trả CÙNG 1 lỗi) ----------

    [Fact]
    public async Task GetForPatientAsync_OwnConfirmedCase_ReturnsPatientResponse()
    {
        // Arrange
        var patientUser = MedicalRecordTestData.MakePatientUser();
        var profile = MedicalRecordTestData.MakePatientProfile(patientUser);
        var medicalCase = MedicalRecordTestData.MakeCase(profile, status: CaseStatus.Confirmed);

        _profiles.Setup(r => r.GetByUserIdAsync(patientUser.UserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);
        _cases.Setup(r => r.GetDetailAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);

        // Act
        var response = await _sut.GetForPatientAsync(medicalCase.CaseId, patientUser.UserId);

        // Assert
        Assert.Equal(medicalCase.CaseId, response.CaseId);
    }

    [Fact]
    public async Task GetForPatientAsync_CallerHasNoProfileYet_ThrowsResourceNotFoundException()
    {
        // Arrange
        _profiles.Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((PatientProfile?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => _sut.GetForPatientAsync(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal("Case not found.", ex.Message);
    }

    [Fact]
    public async Task GetForPatientAsync_CaseBelongsToAnotherPatient_ThrowsResourceNotFoundExceptionWithSameMessage()
    {
        // Arrange — ca tồn tại, đã CONFIRMED, nhưng thuộc hồ sơ khác.
        var callerUser = MedicalRecordTestData.MakePatientUser();
        var callerProfile = MedicalRecordTestData.MakePatientProfile(callerUser);
        var someoneElsesCase = MedicalRecordTestData.MakeCase(status: CaseStatus.Confirmed); // hồ sơ khác

        _profiles.Setup(r => r.GetByUserIdAsync(callerUser.UserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(callerProfile);
        _cases.Setup(r => r.GetDetailAsync(someoneElsesCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(someoneElsesCase);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => _sut.GetForPatientAsync(someoneElsesCase.CaseId, callerUser.UserId));
        Assert.Equal("Case not found.", ex.Message);
    }

    [Fact]
    public async Task GetForPatientAsync_OwnCaseButNotYetConfirmed_ThrowsResourceNotFoundExceptionWithSameMessage()
    {
        // Arrange — GB-05: ca CHƯA duyệt của chính bệnh nhân đó vẫn phải bị giấu, KHÔNG được
        // trả 403 (403 sẽ gián tiếp xác nhận "có tồn tại một ca như vậy").
        var patientUser = MedicalRecordTestData.MakePatientUser();
        var profile = MedicalRecordTestData.MakePatientProfile(patientUser);
        var pendingCase = MedicalRecordTestData.MakeCase(profile, status: CaseStatus.Created);

        _profiles.Setup(r => r.GetByUserIdAsync(patientUser.UserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);
        _cases.Setup(r => r.GetDetailAsync(pendingCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(pendingCase);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => _sut.GetForPatientAsync(pendingCase.CaseId, patientUser.UserId));
        Assert.Equal("Case not found.", ex.Message);
    }

    [Fact]
    public async Task GetForPatientAsync_CaseIdDoesNotExistAtAll_ThrowsResourceNotFoundExceptionWithSameMessage()
    {
        // Arrange — kịch bản thứ 3: ID không tồn tại. Cả 3 message phải giống hệt nhau, để
        // client không phân biệt được 3 tình huống này.
        var patientUser = MedicalRecordTestData.MakePatientUser();
        var profile = MedicalRecordTestData.MakePatientProfile(patientUser);

        _profiles.Setup(r => r.GetByUserIdAsync(patientUser.UserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);
        _cases.Setup(r => r.GetDetailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((Case?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => _sut.GetForPatientAsync(Guid.NewGuid(), patientUser.UserId));
        Assert.Equal("Case not found.", ex.Message);
    }

    // ---------- ListByPatientProfileAsync ----------

    [Fact]
    public async Task ListByPatientProfileAsync_ValidStatusFilter_PassesParsedEnumToRepository()
    {
        // Arrange
        var profile = MedicalRecordTestData.MakePatientProfile();
        _profiles.Setup(r => r.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);
        _cases.Setup(r => r.SearchByPatientAsync(
                  profile.PatientProfileId, CaseStatus.Confirmed, "desc", 1, 20, It.IsAny<CancellationToken>()))
              .ReturnsAsync((new List<Case>(), 0));

        // Act
        await _sut.ListByPatientProfileAsync(profile.PatientProfileId, "confirmed", "desc", 1, 20);

        // Assert
        _cases.Verify(r => r.SearchByPatientAsync(
            profile.PatientProfileId, CaseStatus.Confirmed, "desc", 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListByPatientProfileAsync_InvalidStatusString_ThrowsBusinessException()
    {
        // Arrange
        var profile = MedicalRecordTestData.MakePatientProfile();
        _profiles.Setup(r => r.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);

        // Act & Assert
        await Assert.ThrowsAsync<BusinessException>(
            () => _sut.ListByPatientProfileAsync(profile.PatientProfileId, "BOGUS", "desc", 1, 20));
    }

    [Fact]
    public async Task ListByPatientProfileAsync_PatientProfileNotFound_ThrowsResourceNotFoundException()
    {
        // Arrange
        _profiles.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((PatientProfile?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => _sut.ListByPatientProfileAsync(Guid.NewGuid(), null, "desc", 1, 20));
    }

    // ---------- ListMineAsync ----------

    [Fact]
    public async Task ListMineAsync_AlwaysPassesConfirmedStatusRegardlessOfCaller()
    {
        // Arrange — GB-05: server ép cứng CONFIRMED, không có tham số nào từ client tới được
        // đây để đổi giá trị này.
        var patientUser = MedicalRecordTestData.MakePatientUser();
        var profile = MedicalRecordTestData.MakePatientProfile(patientUser);
        _profiles.Setup(r => r.GetByUserIdAsync(patientUser.UserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);
        _cases.Setup(r => r.SearchByPatientAsync(
                  profile.PatientProfileId, CaseStatus.Confirmed, "desc", 1, 20, It.IsAny<CancellationToken>()))
              .ReturnsAsync((new List<Case>(), 0));

        // Act
        await _sut.ListMineAsync(patientUser.UserId, 1, 20);

        // Assert
        _cases.Verify(r => r.SearchByPatientAsync(
            profile.PatientProfileId, CaseStatus.Confirmed, "desc", 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListMineAsync_CallerHasNoProfile_ThrowsResourceNotFoundException()
    {
        // Arrange
        _profiles.Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((PatientProfile?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => _sut.ListMineAsync(Guid.NewGuid(), 1, 20));
    }

    // ---------- ListImagesAsync ----------

    [Fact]
    public async Task ListImagesAsync_CaseExists_ReturnsMappedImages()
    {
        // Arrange
        var medicalCase = MedicalRecordTestData.MakeCase();
        var image = new UltrasoundImage
        {
            ImageId = Guid.NewGuid(), CaseId = medicalCase.CaseId,
            FileRef = "path/anh.png", UploadedAt = DateTime.UtcNow,
        };
        _cases.Setup(r => r.GetByIdAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);
        _images.Setup(r => r.ListByCaseAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<UltrasoundImage> { image });
        _storage.Setup(s => s.CreateSignedUrlAsync(image.FileRef, It.IsAny<CancellationToken>()))
                .ReturnsAsync("https://signed-url.example/anh.png");

        // Act
        var result = await _sut.ListImagesAsync(medicalCase.CaseId);

        // Assert
        var single = Assert.Single(result);
        Assert.Equal("https://signed-url.example/anh.png", single.ImageUrl);
    }

    [Fact]
    public async Task ListImagesAsync_CaseNotFound_ThrowsResourceNotFoundException()
    {
        // Arrange
        _cases.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((Case?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => _sut.ListImagesAsync(Guid.NewGuid()));
    }
}
