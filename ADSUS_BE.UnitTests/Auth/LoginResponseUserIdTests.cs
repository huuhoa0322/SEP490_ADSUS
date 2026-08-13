using ADSUS_BE.BLL.Auth.Services;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace ADSUS_BE.UnitTests.Auth;

public class LoginResponseUserIdTests
{
    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsUserId()
    {
        // Arrange — FE cần userId để điền sẵn "Bác sĩ phụ trách" khi chính Bác sĩ tạo ca
        // khám. GB-04 không cho backend suy ra từ token, nên giá trị này phải đi kèm lúc
        // đăng nhập, tránh mỗi màn lại phải gọi thêm /users/me.
        var users = new Mock<IUserRepository>();
        var tokens = new Mock<IJwtTokenService>();

        var doctor = new User
        {
            UserId = Guid.NewGuid(),
            Phone = "0913456789",
            FullName = "BS. Lê Minh Hoàng",
            Email = "hoang@example.com",
            Role = UserRole.Doctor,
            Status = UserStatus.Active,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
            MustChangePassword = false,
        };

        users.Setup(r => r.GetByPhoneAsync(doctor.Phone, It.IsAny<CancellationToken>()))
             .ReturnsAsync(doctor);
        tokens.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns("fake-token");

        var sut = new AuthService(users.Object, tokens.Object, new Mock<ILogger<AuthService>>().Object);

        // Act
        var response = await sut.LoginAsync(
            new ADSUS_BE.BLL.Auth.DTOs.LoginRequest { PhoneNumber = doctor.Phone, Password = "password" });

        // Assert
        Assert.NotNull(response);
        Assert.Equal(doctor.UserId, response!.UserId);
    }
}
