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

    private static ADSUS_BE.BLL.MedicalRecord.DTOs.UploadedFile MakeValidPngUpload(string fileName = "anh.png")
    {
        byte[] pngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };
        return new(fileName, "image/png", pngBytes.Length, new MemoryStream(pngBytes));
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
    public async Task ListByPatientProfileAsync_ReturnsCreatedAt()
    {
        // Thêm 06/08/2026 — #24 (StaffCaseSummaryResponse) mang thêm CreatedAt so với #25
        // (CaseSummaryResponse) vì VisitDate là DateOnly, không có giờ; SCR-12 cần hiện giờ
        // tạo ca khám thay cho caseId thô.
        var profile = MedicalRecordTestData.MakePatientProfile();
        var medicalCase = MedicalRecordTestData.MakeCase(profile);
        _profiles.Setup(r => r.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);
        _cases.Setup(r => r.SearchByPatientAsync(
                  profile.PatientProfileId, null, "desc", 1, 20, It.IsAny<CancellationToken>()))
              .ReturnsAsync((new List<Case> { medicalCase }, 1));

        var result = await _sut.ListByPatientProfileAsync(profile.PatientProfileId, null, "desc", 1, 20);

        Assert.Equal(medicalCase.CreatedAt, result.Items.Single().CreatedAt);
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

    // ---------- CreateAsync ----------

    [Fact]
    public async Task CreateAsync_ValidRequestWithOneDoctorAsResponsible_UploadsThenWritesCaseWithCreatedStatus()
    {
        // Arrange
        var profile = MedicalRecordTestData.MakePatientProfile();
        var doctor = MedicalRecordTestData.MakeDoctor();
        var request = new ADSUS_BE.BLL.MedicalRecord.DTOs.CreateCaseRequest(
            profile.PatientProfileId, doctor.UserId, "Đau vú trái", new[] { MakeValidPngUpload() });

        _profiles.Setup(r => r.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);
        _users.Setup(r => r.GetByIdAsync(doctor.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(doctor);
        _storage.Setup(s => s.UploadAsync(
                    It.IsAny<Stream>(), It.IsAny<string>(), "image/png", It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream _, string path, string _, CancellationToken _) => path);

        Case? createdCase = null;
        _cases.Setup(r => r.CreateWithImagesAsync(
                  It.IsAny<Case>(), It.IsAny<IReadOnlyList<UltrasoundImage>>(), It.IsAny<CancellationToken>()))
              .Callback<Case, IReadOnlyList<UltrasoundImage>, CancellationToken>((c, imgs, _) =>
              {
                  createdCase = c;
                  c.UltrasoundImages = imgs.ToList();
                  c.PatientProfile = profile;
                  c.Doctor = doctor;
              })
              .ReturnsAsync((Case c, IReadOnlyList<UltrasoundImage> _, CancellationToken _) => c);
        _cases.Setup(r => r.GetDetailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(() => createdCase);

        // Act
        var response = await _sut.CreateAsync(request);

        // Assert
        Assert.Equal("CREATED", response.Status);
        Assert.Equal(doctor.UserId, response.DoctorId);
        Assert.Single(response.UltrasoundImages);
        _storage.Verify(s => s.UploadAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), "image/png", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_NoImagesAttached_SucceedsWithEmptyImageListAndSkipsStorage()
    {
        // Arrange — quyết định ghi đè 07/08/2026: #20 không còn bắt buộc ảnh nữa. Không có ảnh
        // nào thì đơn giản là không gọi Storage lần nào (không phải lỗi).
        var profile = MedicalRecordTestData.MakePatientProfile();
        var doctor = MedicalRecordTestData.MakeDoctor();
        var request = new ADSUS_BE.BLL.MedicalRecord.DTOs.CreateCaseRequest(
            profile.PatientProfileId, doctor.UserId, "Đau vú trái",
            Array.Empty<ADSUS_BE.BLL.MedicalRecord.DTOs.UploadedFile>());

        _profiles.Setup(r => r.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);
        _users.Setup(r => r.GetByIdAsync(doctor.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(doctor);

        Case? createdCase = null;
        _cases.Setup(r => r.CreateWithImagesAsync(
                  It.IsAny<Case>(), It.IsAny<IReadOnlyList<UltrasoundImage>>(), It.IsAny<CancellationToken>()))
              .Callback<Case, IReadOnlyList<UltrasoundImage>, CancellationToken>((c, imgs, _) =>
              {
                  createdCase = c;
                  c.UltrasoundImages = imgs.ToList();
                  c.PatientProfile = profile;
                  c.Doctor = doctor;
              })
              .ReturnsAsync((Case c, IReadOnlyList<UltrasoundImage> _, CancellationToken _) => c);
        _cases.Setup(r => r.GetDetailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(() => createdCase);

        // Act
        var response = await _sut.CreateAsync(request);

        // Assert
        Assert.Equal("CREATED", response.Status);
        Assert.Empty(response.UltrasoundImages);
        _storage.Verify(s => s.UploadAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_PatientProfileNotFound_ThrowsResourceNotFoundException()
    {
        // Arrange
        _profiles.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((PatientProfile?)null);
        var request = new ADSUS_BE.BLL.MedicalRecord.DTOs.CreateCaseRequest(
            Guid.NewGuid(), Guid.NewGuid(), null, new[] { MakeValidPngUpload() });

        // Act & Assert
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => _sut.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_ResponsibleDoctorIdNotFound_ThrowsResourceNotFoundException()
    {
        // Arrange
        var profile = MedicalRecordTestData.MakePatientProfile();
        _profiles.Setup(r => r.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);
        _users.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((User?)null);
        var request = new ADSUS_BE.BLL.MedicalRecord.DTOs.CreateCaseRequest(
            profile.PatientProfileId, Guid.NewGuid(), null, new[] { MakeValidPngUpload() });

        // Act & Assert
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => _sut.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_ResponsibleDoctorIdBelongsToNonDoctorAccount_ThrowsBusinessException()
    {
        // Arrange — GB-04: người phụ trách bắt buộc là tài khoản role DOCTOR, kể cả khi Điều
        // dưỡng đang tạo ca hộ.
        var profile = MedicalRecordTestData.MakePatientProfile();
        var nurse = MedicalRecordTestData.MakeNurse();
        _profiles.Setup(r => r.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);
        _users.Setup(r => r.GetByIdAsync(nurse.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(nurse);
        var request = new ADSUS_BE.BLL.MedicalRecord.DTOs.CreateCaseRequest(
            profile.PatientProfileId, nurse.UserId, null, new[] { MakeValidPngUpload() });

        // Act & Assert
        await Assert.ThrowsAsync<BusinessException>(() => _sut.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_ImageFailsContentValidation_ThrowsWithoutCallingStorageUpload()
    {
        // Arrange — file giả (không đúng magic byte JPEG/PNG) phải bị chặn TRƯỚC khi upload
        // bất kỳ byte nào lên Storage.
        var profile = MedicalRecordTestData.MakePatientProfile();
        var doctor = MedicalRecordTestData.MakeDoctor();
        var fakeFile = new ADSUS_BE.BLL.MedicalRecord.DTOs.UploadedFile(
            "fake.jpg", "image/jpeg", 10, new MemoryStream("khong-phai-anh"u8.ToArray()));
        var request = new ADSUS_BE.BLL.MedicalRecord.DTOs.CreateCaseRequest(
            profile.PatientProfileId, doctor.UserId, null, new[] { fakeFile });

        _profiles.Setup(r => r.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);
        _users.Setup(r => r.GetByIdAsync(doctor.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(doctor);

        // Act & Assert
        await Assert.ThrowsAsync<BusinessException>(() => _sut.CreateAsync(request));
        _storage.Verify(s => s.UploadAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_DatabaseWriteFailsAfterUpload_DeletesUploadedObjectBeforeRethrowing()
    {
        // Arrange — đây là test khoá lại quy tắc quan trọng nhất của Task 7: upload trước,
        // ghi DB sau; DB hỏng thì phải dọn Storage rồi mới ném lại lỗi gốc.
        var profile = MedicalRecordTestData.MakePatientProfile();
        var doctor = MedicalRecordTestData.MakeDoctor();
        var request = new ADSUS_BE.BLL.MedicalRecord.DTOs.CreateCaseRequest(
            profile.PatientProfileId, doctor.UserId, null, new[] { MakeValidPngUpload() });

        _profiles.Setup(r => r.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);
        _users.Setup(r => r.GetByIdAsync(doctor.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(doctor);

        string? uploadedPath = null;
        _storage.Setup(s => s.UploadAsync(
                    It.IsAny<Stream>(), It.IsAny<string>(), "image/png", It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream _, string path, string _, CancellationToken _) =>
                {
                    uploadedPath = path;
                    return path;
                });
        _cases.Setup(r => r.CreateWithImagesAsync(
                  It.IsAny<Case>(), It.IsAny<IReadOnlyList<UltrasoundImage>>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("giả lập DB hỏng"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateAsync(request));
        Assert.NotNull(uploadedPath);
        _storage.Verify(s => s.DeleteAsync(uploadedPath!, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- AddImagesAsync ----------

    [Fact]
    public async Task AddImagesAsync_CaseNotYetConfirmed_UploadsAndAppendsImages()
    {
        // Arrange
        var medicalCase = MedicalRecordTestData.MakeCase(status: CaseStatus.Created);
        var request = new ADSUS_BE.BLL.MedicalRecord.DTOs.AddUltrasoundImagesRequest(
            new[] { MakeValidPngUpload() }, "Ảnh bổ sung");

        _cases.Setup(r => r.GetByIdAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);
        _storage.Setup(s => s.UploadAsync(
                    It.IsAny<Stream>(), It.IsAny<string>(), "image/png", It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream _, string path, string _, CancellationToken _) => path);

        // Act
        var result = await _sut.AddImagesAsync(medicalCase.CaseId, request);

        // Assert
        Assert.Single(result);
        Assert.Equal("Ảnh bổ sung", result[0].Note);
        _images.Verify(r => r.AddRangeAsync(
            It.IsAny<IReadOnlyList<UltrasoundImage>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddImagesAsync_CaseAlreadyConfirmed_ThrowsBusinessExceptionWithoutUploading()
    {
        // Arrange — GB-01: ca đã chốt không mở lại để nhận thêm ảnh.
        var confirmedCase = MedicalRecordTestData.MakeCase(status: CaseStatus.Confirmed);
        var request = new ADSUS_BE.BLL.MedicalRecord.DTOs.AddUltrasoundImagesRequest(
            new[] { MakeValidPngUpload() }, null);
        _cases.Setup(r => r.GetByIdAsync(confirmedCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(confirmedCase);

        // Act & Assert
        await Assert.ThrowsAsync<BusinessException>(
            () => _sut.AddImagesAsync(confirmedCase.CaseId, request));
        _storage.Verify(s => s.UploadAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddImagesAsync_CaseNotFound_ThrowsResourceNotFoundException()
    {
        // Arrange
        _cases.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((Case?)null);
        var request = new ADSUS_BE.BLL.MedicalRecord.DTOs.AddUltrasoundImagesRequest(
            new[] { MakeValidPngUpload() }, null);

        // Act & Assert
        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => _sut.AddImagesAsync(Guid.NewGuid(), request));
    }

    [Fact]
    public async Task AddImagesAsync_DatabaseWriteFailsAfterUpload_DeletesUploadedObjectBeforeRethrowing()
    {
        // Arrange — cùng quy tắc rollback với CreateAsync, áp cho #21.
        var medicalCase = MedicalRecordTestData.MakeCase(status: CaseStatus.Created);
        var request = new ADSUS_BE.BLL.MedicalRecord.DTOs.AddUltrasoundImagesRequest(
            new[] { MakeValidPngUpload() }, null);
        _cases.Setup(r => r.GetByIdAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);

        string? uploadedPath = null;
        _storage.Setup(s => s.UploadAsync(
                    It.IsAny<Stream>(), It.IsAny<string>(), "image/png", It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream _, string path, string _, CancellationToken _) =>
                {
                    uploadedPath = path;
                    return path;
                });
        _images.Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyList<UltrasoundImage>>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new InvalidOperationException("giả lập DB hỏng"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AddImagesAsync(medicalCase.CaseId, request));
        Assert.NotNull(uploadedPath);
        _storage.Verify(s => s.DeleteAsync(uploadedPath!, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddImagesAsync_StorageDeleteDuringCleanupAlsoFails_OriginalExceptionStillPropagates()
    {
        // Arrange — test khoá lại fix thứ 2 của Task 7 review: dọn dẹp thất bại không được
        // che mất lỗi gốc đã kích hoạt việc dọn dẹp.
        var medicalCase = MedicalRecordTestData.MakeCase(status: CaseStatus.Created);
        var request = new ADSUS_BE.BLL.MedicalRecord.DTOs.AddUltrasoundImagesRequest(
            new[] { MakeValidPngUpload() }, null);
        _cases.Setup(r => r.GetByIdAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);
        _storage.Setup(s => s.UploadAsync(
                    It.IsAny<Stream>(), It.IsAny<string>(), "image/png", It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream _, string path, string _, CancellationToken _) => path);
        _images.Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyList<UltrasoundImage>>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new InvalidOperationException("loi DB goc"));
        _storage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("loi xoa Storage"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AddImagesAsync(medicalCase.CaseId, request));
        Assert.Equal("loi DB goc", ex.Message);
    }
}
