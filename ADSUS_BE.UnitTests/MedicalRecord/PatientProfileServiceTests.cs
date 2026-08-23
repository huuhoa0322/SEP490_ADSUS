using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace ADSUS_BE.UnitTests.MedicalRecord;

public class PatientProfileServiceTests
{
    private readonly Mock<IPatientProfileRepository> _profiles = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly PatientProfileService _sut;

    public PatientProfileServiceTests()
    {
        _sut = new PatientProfileService(
            _profiles.Object, _users.Object, Mock.Of<ILogger<PatientProfileService>>());
    }

    // ---------- CreateAsync ----------

    [Fact]
    public async Task CreateAsync_ValidPatientAccount_CreatesProfileWithActingUserAsCreatedBy()
    {
        // Arrange
        var patient = MedicalRecordTestData.MakePatientUser();
        var actingDoctorId = Guid.NewGuid();
        var request = new CreatePatientProfileRequest(patient.UserId, "FEMALE", new List<PatientDiseaseInput>(), new List<PatientAllergyInput>());

        _users.Setup(r => r.GetByIdAsync(patient.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(patient);
        _profiles.Setup(r => r.ExistsForUserAsync(patient.UserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);

        PatientProfile? saved = null;
        _profiles.Setup(r => r.AddAsync(It.IsAny<PatientProfile>(), It.IsAny<CancellationToken>()))
                 .Callback<PatientProfile, CancellationToken>((p, _) => saved = p)
                 .ReturnsAsync((PatientProfile p, CancellationToken _) => p);
        _profiles.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(() => saved); // PatientProfile là class, không phải record — không dùng `with`

        // Act
        var response = await _sut.CreateAsync(request, actingDoctorId);

        // Assert
        Assert.Equal(patient.UserId, response.PatientUserId);
        Assert.Equal(actingDoctorId, saved!.CreatedBy);
    }

    [Fact]
    public async Task CreateAsync_NurseIsActingUser_CreatedByStillRecordsNurse()
    {
        // Arrange — UC-06: Điều dưỡng cũng được phép lập hồ sơ, không chỉ Bác sĩ.
        var patient = MedicalRecordTestData.MakePatientUser();
        var actingNurseId = Guid.NewGuid();
        var request = new CreatePatientProfileRequest(patient.UserId, "FEMALE", new List<PatientDiseaseInput>(), new List<PatientAllergyInput>());

        _users.Setup(r => r.GetByIdAsync(patient.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(patient);
        _profiles.Setup(r => r.ExistsForUserAsync(patient.UserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);

        PatientProfile? saved = null;
        _profiles.Setup(r => r.AddAsync(It.IsAny<PatientProfile>(), It.IsAny<CancellationToken>()))
                 .Callback<PatientProfile, CancellationToken>((p, _) => saved = p)
                 .ReturnsAsync((PatientProfile p, CancellationToken _) => p);
        _profiles.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(() => saved);

        // Act
        await _sut.CreateAsync(request, actingNurseId);

        // Assert
        Assert.Equal(actingNurseId, saved!.CreatedBy);
    }

    [Fact]
    public async Task CreateAsync_PatientUserIdNotFound_ThrowsResourceNotFoundException()
    {
        // Arrange
        var request = new CreatePatientProfileRequest(Guid.NewGuid(), "FEMALE", new List<PatientDiseaseInput>(), new List<PatientAllergyInput>());
        _users.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((User?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => _sut.CreateAsync(request, Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateAsync_AccountRoleIsNotPatient_ThrowsBusinessException()
    {
        // Arrange — BR-01: tài khoản đích phải có role PATIENT.
        var doctorAccount = MedicalRecordTestData.MakeDoctor();
        var request = new CreatePatientProfileRequest(doctorAccount.UserId, "FEMALE", new List<PatientDiseaseInput>(), new List<PatientAllergyInput>());
        _users.Setup(r => r.GetByIdAsync(doctorAccount.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(doctorAccount);

        // Act & Assert
        await Assert.ThrowsAsync<BusinessException>(() => _sut.CreateAsync(request, Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateAsync_ProfileAlreadyExistsForUser_ThrowsConflictException()
    {
        // Arrange — uq_patient_profiles_user: 1 tài khoản chỉ có đúng 1 hồ sơ nền.
        var patient = MedicalRecordTestData.MakePatientUser();
        var request = new CreatePatientProfileRequest(patient.UserId, "FEMALE", new List<PatientDiseaseInput>(), new List<PatientAllergyInput>());
        _users.Setup(r => r.GetByIdAsync(patient.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(patient);
        _profiles.Setup(r => r.ExistsForUserAsync(patient.UserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => _sut.CreateAsync(request, Guid.NewGuid()));
    }

    // ---------- UpdateAsync ----------

    [Fact]
    public async Task UpdateAsync_ExistingProfile_UpdatesFieldsAndReturnsResponse()
    {
        // Arrange
        var profile = MedicalRecordTestData.MakePatientProfile();
        var request = new UpdatePatientProfileRequest("MALE", new List<PatientDiseaseInput>(), new List<PatientAllergyInput>());
        _profiles.Setup(r => r.GetForUpdateAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);

        // Act
        var response = await _sut.UpdateAsync(profile.PatientProfileId, request);

        // Assert
        Assert.Equal("MALE", response.Gender);
        Assert.Empty(response.Diseases);
        _profiles.Verify(r => r.UpdateAsync(profile, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ProfileNotFound_ThrowsResourceNotFoundException()
    {
        // Arrange
        _profiles.Setup(r => r.GetForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((PatientProfile?)null);
        var request = new UpdatePatientProfileRequest("MALE", new List<PatientDiseaseInput>(), new List<PatientAllergyInput>());

        // Act & Assert
        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => _sut.UpdateAsync(Guid.NewGuid(), request));
    }

    // ---------- GetByIdAsync ----------

    [Fact]
    public async Task GetByIdAsync_ExistingProfile_ReturnsResponse()
    {
        // Arrange
        var profile = MedicalRecordTestData.MakePatientProfile();
        _profiles.Setup(r => r.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);

        // Act
        var response = await _sut.GetByIdAsync(profile.PatientProfileId);

        // Assert
        Assert.Equal(profile.PatientProfileId, response.PatientProfileId);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ThrowsResourceNotFoundException()
    {
        // Arrange
        _profiles.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((PatientProfile?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => _sut.GetByIdAsync(Guid.NewGuid()));
    }

    // ---------- SearchPatientsAsync ----------

    [Fact]
    public async Task SearchPatientsAsync_ReturnsPagedResultWithComputedTotalPages()
    {
        // Arrange
        var rows = new List<PatientListRow>
        {
            new(PatientProfileId: Guid.NewGuid(),
                PatientUserId: Guid.NewGuid(),
                FullName: "Trần Thị Mai",
                Phone: "0987654321",
                LatestVisitDate: new DateOnly(2026, 7, 22),
                LatestVisitStatus: "CONFIRMED"),
        };
        _profiles.Setup(r => r.SearchAsync("hoa", null, null, 1, 20, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((rows, 1));

        // Act
        var result = await _sut.SearchPatientsAsync("hoa", null, null, 1, 20);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalItems);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task SearchPatientsAsync_NoResults_TotalPagesIsAtLeastOne()
    {
        // Arrange — tránh chia cho 0 / trang 0 khiến UI phải xử lý trường hợp đặc biệt.
        _profiles.Setup(r => r.SearchAsync(
                     It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool?>(), 1, 20, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((new List<PatientListRow>(), 0));

        // Act
        var result = await _sut.SearchPatientsAsync(null, null, null, 1, 20);

        // Assert
        Assert.Empty(result.Items);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task SearchPatientsAsync_PassesHasProfileFilterThroughToRepository()
    {
        // Arrange — luồng tạo hồ sơ nền (#17) gọi với hasProfile=false để chỉ lấy tài khoản
        // chưa có hồ sơ. Nếu service nuốt mất tham số này thì màn chọn tài khoản sẽ hiện cả
        // người đã có hồ sơ, và bấm tạo sẽ nhận 409.
        _profiles.Setup(r => r.SearchAsync(null, null, false, 1, 20, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((new List<PatientListRow>(), 0));

        // Act
        await _sut.SearchPatientsAsync(null, null, false, 1, 20);

        // Assert
        _profiles.Verify(r => r.SearchAsync(null, null, false, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchPatientsAsync_AccountWithoutProfile_ReturnsNullPatientProfileId()
    {
        // Arrange
        var rows = new List<PatientListRow>
        {
            new(PatientProfileId: null,
                PatientUserId: Guid.NewGuid(),
                FullName: "Lê Thị Hoa",
                Phone: "0978123456",
                LatestVisitDate: null,
                LatestVisitStatus: null),
        };
        _profiles.Setup(r => r.SearchAsync(null, null, false, 1, 20, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((rows, 1));

        // Act
        var result = await _sut.SearchPatientsAsync(null, null, false, 1, 20);

        // Assert — giao diện dựa vào null này để đổi nút thành "Tạo hồ sơ nền".
        Assert.Null(result.Items.Single().PatientProfileId);
    }
}
