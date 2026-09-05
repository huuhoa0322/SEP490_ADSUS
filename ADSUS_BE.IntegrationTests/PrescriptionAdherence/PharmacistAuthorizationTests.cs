using System.Net;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using ADSUS_BE.IntegrationTests.AppointmentScheduling;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ADSUS_BE.IntegrationTests.PrescriptionAdherence;

public class PharmacistAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<IUserRepository> _users;

    public PharmacistAuthorizationTests(WebApplicationFactory<Program> factory)
    {
        _users = new Mock<IUserRepository>();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Thay thế repository thật bằng mock để TestAuthHelper hoạt động
                services.AddScoped(_ => _users.Object);
            });
        });
    }

    [Theory]
    [InlineData("/api/v1/medicines/admin")]
    [InlineData("/api/v1/medicines/units")]
    [InlineData("/api/v1/inventory/history")]
    [InlineData("/api/v1/inventory/alerts")]
    [InlineData("/api/v1/suppliers")]
    public async Task Pharmacist_CanAccess_MedicineEndpoints(string url)
    {
        // Arrange
        var client = TestAuthHelper.CreatePharmacistClient(_factory, _users);

        // Act
        var response = await client.GetAsync(url);

        // Assert
        // Nên trả về 200 OK, nhưng nếu dữ liệu mock không đủ thì có thể trả 404/400.
        // Điều quan trọng là Authorization check đã qua (không phải 401 hoặc 403).
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/dashboard/statistics")]
    [InlineData("/api/v1/auditlogs")]
    [InlineData("/api/v1/admin/users")]
    [InlineData("/api/v1/aimodels")]
    [InlineData("/api/v1/admin/blog/posts")]
    public async Task Pharmacist_CannotAccess_OtherAdminEndpoints(string url)
    {
        // Arrange
        var client = TestAuthHelper.CreatePharmacistClient(_factory, _users);

        // Act
        var response = await client.GetAsync(url);

        // Assert
        // Pharmacist không được cấp quyền cho các API không liên quan tới thuốc
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/medicines/admin")]
    [InlineData("/api/v1/inventory/history")]
    [InlineData("/api/v1/inventory/alerts")]
    [InlineData("/api/v1/suppliers")]
    public async Task DoctorNursePatient_CannotAccess_MedicineAdminEndpoints(string url)
    {
        // Arrange
        var doctorClient = TestAuthHelper.CreateDoctorClient(_factory, _users);
        var nurseClient = TestAuthHelper.CreateNurseClient(_factory, _users);
        var patientClient = TestAuthHelper.CreatePatientClient(_factory, _users);

        // Act & Assert
        var doctorResponse = await doctorClient.GetAsync(url);
        Assert.Equal(HttpStatusCode.Forbidden, doctorResponse.StatusCode);

        var nurseResponse = await nurseClient.GetAsync(url);
        Assert.Equal(HttpStatusCode.Forbidden, nurseResponse.StatusCode);

        var patientResponse = await patientClient.GetAsync(url);
        Assert.Equal(HttpStatusCode.Forbidden, patientResponse.StatusCode);
    }
}
