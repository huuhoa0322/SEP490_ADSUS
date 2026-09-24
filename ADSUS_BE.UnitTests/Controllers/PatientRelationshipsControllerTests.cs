using System.Reflection;
using System.Security.Claims;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.PatientRelationship.DTOs;
using ADSUS_BE.BLL.PatientRelationship.Interfaces;
using ADSUS_BE.Controllers;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.Controllers;

public class PatientRelationshipsControllerTests : IDisposable
{
    private readonly Mock<IPatientRelationshipService> _serviceMock;
    private readonly AppDbContext _db;
    private readonly PatientRelationshipsController _controller;

    public PatientRelationshipsControllerTests()
    {
        _serviceMock = new Mock<IPatientRelationshipService>();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PatientRelationshipsTestDb_{Guid.NewGuid()}")
            .Options;
        _db = new AppDbContext(options);

        _controller = new PatientRelationshipsController(_serviceMock.Object);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    #region AddRelativeForGuardian Tests

    [Fact]
    public async Task AddRelativeForGuardian_GuardianUserNotFound_Returns404NotFound()
    {
        // Arrange
        var nonExistentGuardianId = Guid.NewGuid();
        var request = new AddRelativeRequest("Nguyễn Văn Con", "0912345678", new DateOnly(2015, 5, 20), "Con");

        // Act
        var result = await _controller.AddRelativeForGuardian(nonExistentGuardianId, request, CancellationToken.None);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);

        var response = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
        Assert.Equal(404, response.Code);
        Assert.Equal("Không tìm thấy tài khoản bệnh nhân.", response.Message);
        _serviceMock.Verify(s => s.AddRelativeAsync(It.IsAny<AddRelativeRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddRelativeForGuardian_GuardianExistsButNotPatientRole_Returns404NotFound()
    {
        // Arrange
        var nonPatientId = Guid.NewGuid();
        _db.Users.Add(new User
        {
            UserId = nonPatientId,
            FullName = "Dr. Nguyễn",
            Phone = "0988111222",
            PasswordHash = "hash",
            Role = UserRole.Doctor,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var request = new AddRelativeRequest("Nguyễn Văn Con", "0912345678", new DateOnly(2015, 5, 20), "Con");

        // Act
        var result = await _controller.AddRelativeForGuardian(nonPatientId, request, CancellationToken.None);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);

        var response = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
        Assert.Equal(404, response.Code);
        Assert.Equal("Không tìm thấy tài khoản bệnh nhân.", response.Message);
        _serviceMock.Verify(s => s.AddRelativeAsync(It.IsAny<AddRelativeRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddRelativeForGuardian_PhoneRegistered_Returns400BadRequest()
    {
        // Arrange
        var guardianId = Guid.NewGuid();
        _db.Users.Add(new User
        {
            UserId = guardianId,
            FullName = "Nguyễn Mẹ",
            Phone = "0987654321",
            PasswordHash = "hash",
            Role = UserRole.Patient,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var request = new AddRelativeRequest("Nguyễn Bố", "0988888888", new DateOnly(1980, 1, 1), "Chồng");
        var errorMessage = "Số điện thoại này đã có tài khoản trong hệ thống. Người thân vui lòng đăng nhập bằng tài khoản riêng để đặt lịch.";

        _serviceMock.Setup(s => s.AddRelativeForGuardianAsync(request, guardianId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(errorMessage));

        // Act
        var result = await _controller.AddRelativeForGuardian(guardianId, request, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);

        var response = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.Equal(400, response.Code);
        Assert.Equal(errorMessage, response.Message);
    }

    [Fact]
    public async Task AddRelativeForGuardian_DuplicateRelative_Returns400BadRequest()
    {
        // Arrange
        var guardianId = Guid.NewGuid();
        _db.Users.Add(new User
        {
            UserId = guardianId,
            FullName = "Nguyễn Mẹ",
            Phone = "0987654321",
            PasswordHash = "hash",
            Role = UserRole.Patient,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var request = new AddRelativeRequest("Nguyễn Bé", null, new DateOnly(2020, 2, 2), "Con");
        var errorMessage = "Người thân này đã có trong danh bạ của bạn.";

        _serviceMock.Setup(s => s.AddRelativeForGuardianAsync(request, guardianId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(errorMessage));

        // Act
        var result = await _controller.AddRelativeForGuardian(guardianId, request, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);

        var response = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.Equal(400, response.Code);
        Assert.Equal(errorMessage, response.Message);
    }

    [Fact]
    public async Task AddRelativeForGuardian_CreationSucceeds_Returns200OkWithData()
    {
        // Arrange
        var guardianId = Guid.NewGuid();
        _db.Users.Add(new User
        {
            UserId = guardianId,
            FullName = "Trần Thị Mẹ",
            Phone = "0912345678",
            PasswordHash = "hash",
            Role = UserRole.Patient,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var request = new AddRelativeRequest("Trần Con", "0911222333", new DateOnly(2018, 8, 15), "Con");
        var expectedResponse = new RelativeResponse(
            RelationshipId: Guid.NewGuid(),
            PatientProfileId: Guid.NewGuid(),
            PatientName: "Trần Con",
            PatientPhone: "0911222333",
            DateOfBirth: new DateOnly(2018, 8, 15),
            Gender: "Nam",
            RelationshipName: "Con",
            IsRegisteredAccount: false,
            CreatedAt: DateTime.UtcNow
        );

        _serviceMock.Setup(s => s.AddRelativeForGuardianAsync(request, guardianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.AddRelativeForGuardian(guardianId, request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var response = Assert.IsType<ApiResponse<RelativeResponse>>(okResult.Value);
        Assert.Equal(200, response.Code);
        Assert.NotNull(response.Data);
        Assert.Equal(expectedResponse.RelationshipId, response.Data.RelationshipId);
        Assert.Equal(expectedResponse.PatientName, response.Data.PatientName);
        Assert.Equal(expectedResponse.RelationshipName, response.Data.RelationshipName);
    }

    #endregion

    #region GetRelativesForGuardian Tests

    [Fact]
    public async Task GetRelativesForGuardian_Returns200OkWithRelativesList()
    {
        // Arrange
        var guardianId = Guid.NewGuid();
        var relative1 = new RelativeResponse(
            RelationshipId: Guid.NewGuid(),
            PatientProfileId: Guid.NewGuid(),
            PatientName: "Bé Một",
            PatientPhone: null,
            DateOfBirth: new DateOnly(2021, 1, 1),
            Gender: "Nam",
            RelationshipName: "Con",
            IsRegisteredAccount: false,
            CreatedAt: DateTime.UtcNow
        );
        var relative2 = new RelativeResponse(
            RelationshipId: Guid.NewGuid(),
            PatientProfileId: Guid.NewGuid(),
            PatientName: "Bé Hai",
            PatientPhone: null,
            DateOfBirth: new DateOnly(2023, 3, 3),
            Gender: "Nữ",
            RelationshipName: "Con",
            IsRegisteredAccount: false,
            CreatedAt: DateTime.UtcNow
        );

        var expectedList = new RelativesListResponse(new List<RelativeResponse> { relative1, relative2 });

        _serviceMock.Setup(s => s.GetRelativesAsync(guardianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedList);

        // Act
        var result = await _controller.GetRelativesForGuardian(guardianId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var response = Assert.IsType<ApiResponse<RelativesListResponse>>(okResult.Value);
        Assert.Equal(200, response.Code);
        Assert.NotNull(response.Data);
        Assert.Equal(2, response.Data.Relatives.Count);
        Assert.Equal("Bé Một", response.Data.Relatives[0].PatientName);
        Assert.Equal("Bé Hai", response.Data.Relatives[1].PatientName);
    }

    #endregion

    #region DeleteRelative Tests

    [Fact]
    public async Task DeleteRelative_Returns400BadRequest_WhenDisallowed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var relId = Guid.NewGuid();
        var expectedMessage = "Không được phép xóa người thân để bảo đảm tính toàn vẹn của hồ sơ và lịch sử ca khám bệnh.";

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                }))
            }
        };

        _serviceMock.Setup(s => s.DeleteRelativeAsync(relId, userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(expectedMessage));

        // Act
        var result = await _controller.DeleteRelative(relId, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);

        var val = badRequestResult.Value!;
        var messageProp = val.GetType().GetProperty("message");
        Assert.NotNull(messageProp);
        var message = messageProp.GetValue(val)?.ToString();
        Assert.Equal(expectedMessage, message);
    }

    #endregion

    #region Reflection Authorization Tests

    [Fact]
    public void PatientRelationshipsController_ClassLevelAuthorize_DoesNotRestrictToPatient()
    {
        // Assert class has [Authorize]
        var classAuthorizeAttr = typeof(PatientRelationshipsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(classAuthorizeAttr);

        // Assert Roles does NOT restrict to "PATIENT" (should be empty or null so staff can access staff endpoints)
        Assert.True(string.IsNullOrEmpty(classAuthorizeAttr.Roles),
            $"Class-level AuthorizeAttribute should not restrict roles, but found: {classAuthorizeAttr.Roles}");
    }

    [Theory]
    [InlineData(nameof(PatientRelationshipsController.GetRelatives))]
    [InlineData(nameof(PatientRelationshipsController.GetRelative))]
    [InlineData(nameof(PatientRelationshipsController.AddRelative))]
    [InlineData(nameof(PatientRelationshipsController.UpdateRelative))]
    [InlineData(nameof(PatientRelationshipsController.DeleteRelative))]
    public void PatientRelationshipsController_PatientActions_HaveAuthorizePatientRole(string actionName)
    {
        var method = typeof(PatientRelationshipsController).GetMethod(actionName);
        Assert.NotNull(method);

        var authorizeAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorizeAttr);
        Assert.Equal("PATIENT", authorizeAttr.Roles);
    }

    [Theory]
    [InlineData(nameof(PatientRelationshipsController.AddRelativeForGuardian))]
    [InlineData(nameof(PatientRelationshipsController.GetRelativesForGuardian))]
    public void PatientRelationshipsController_GuardianActions_HaveAuthorizeStaffAdminRoles(string actionName)
    {
        var method = typeof(PatientRelationshipsController).GetMethod(actionName);
        Assert.NotNull(method);

        var authorizeAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorizeAttr);
        Assert.Equal("STAFF,ADMIN", authorizeAttr.Roles);
    }

    [Fact]
    public void PatientRelationshipsController_CheckPhone_HasAllowAnonymous()
    {
        var method = typeof(PatientRelationshipsController).GetMethod(nameof(PatientRelationshipsController.CheckPhone));
        Assert.NotNull(method);

        var allowAnonymousAttr = method.GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.NotNull(allowAnonymousAttr);
    }

    [Fact]
    public void BackendControllers_NoActionContainsReceptionistRole()
    {
        var backendAssembly = typeof(PatientRelationshipsController).Assembly;
        var controllerTypes = backendAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var controller in controllerTypes)
        {
            var classAuth = controller.GetCustomAttribute<AuthorizeAttribute>();
            if (classAuth?.Roles != null)
            {
                Assert.False(classAuth.Roles.Contains("RECEPTIONIST", StringComparison.OrdinalIgnoreCase),
                    $"Controller {controller.Name} class-level Authorize contains RECEPTIONIST: {classAuth.Roles}");
            }

            var methods = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                var methodAuth = method.GetCustomAttribute<AuthorizeAttribute>();
                if (methodAuth?.Roles != null)
                {
                    Assert.False(methodAuth.Roles.Contains("RECEPTIONIST", StringComparison.OrdinalIgnoreCase),
                        $"Controller {controller.Name}, Method {method.Name} Authorize contains RECEPTIONIST: {methodAuth.Roles}");
                }
            }
        }
    }

    #endregion
}
