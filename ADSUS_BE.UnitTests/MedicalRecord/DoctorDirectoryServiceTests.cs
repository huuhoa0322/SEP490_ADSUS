using ADSUS_BE.BLL.MedicalRecord.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Moq;

namespace ADSUS_BE.UnitTests.MedicalRecord;

public class DoctorDirectoryServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly DoctorDirectoryService _sut;

    public DoctorDirectoryServiceTests() => _sut = new DoctorDirectoryService(_users.Object);

    [Fact]
    public async Task ListAsync_ReturnsOnlyIdAndFullName()
    {
        // Arrange — DTO cố ý KHÔNG có email/trạng thái tài khoản: đó là dữ liệu quản trị của
        // Module 2, không thuộc góc nhìn lâm sàng (cùng lý do đã tách /patients khỏi
        // /admin/users). Test này khoá lại hình dạng đó.
        var doctor = MedicalRecordTestData.MakeDoctor();
        _users.Setup(r => r.ListActiveDoctorsAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(new List<User> { doctor });

        // Act
        var result = await _sut.ListAsync();

        // Assert
        var row = Assert.Single(result);
        Assert.Equal(doctor.UserId, row.UserId);
        Assert.Equal(doctor.FullName, row.FullName);

        var propertyNames = typeof(ADSUS_BE.BLL.MedicalRecord.DTOs.DoctorSummaryResponse)
            .GetProperties()
            .Select(p => p.Name)
            .Where(n => n != "EqualityContract")
            .ToArray();
        Assert.Equal(new[] { "UserId", "FullName" }, propertyNames);
    }

    [Fact]
    public async Task ListAsync_NoDoctors_ReturnsEmptyListNotNull()
    {
        // Arrange — giao diện render .map() trên kết quả; null sẽ làm vỡ màn tạo ca khám.
        _users.Setup(r => r.ListActiveDoctorsAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(new List<User>());

        // Act
        var result = await _sut.ListAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
