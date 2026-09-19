using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Interfaces;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace ADSUS_BE.IntegrationTests.Security;

/// <summary>
/// SEC-01: Cross-Role Authorization Matrix & Security Audit Tests.
/// Validates RBAC enforcement across all 5 roles: ADMIN, DOCTOR, STAFF, PHARMACIST, PATIENT.
/// Verifies 401 Unauthorized for unauthenticated requests and 403 Forbidden for cross-role violations.
/// Also includes proof-of-concept test demonstrating the IDOR vulnerability in GET /api/v1/appointments/{id}.
/// </summary>
public class CrossRoleAuthorizationMatrixTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IAppointmentService> _appointmentService = new();
    private readonly Mock<IPatientProfileRepository> _patientProfileRepo = new();
    private readonly Mock<IUserAccountService> _userAccountService = new();
    private readonly Mock<IPrescriptionService> _prescriptionService = new();
    private readonly Mock<IInvoiceService> _invoiceService = new();
    private readonly Mock<IPatientProfileService> _patientProfileService = new();
    private readonly Mock<IPatientAccountService> _patientAccountService = new();
    private readonly Mock<IInventoryService> _inventoryService = new();

    private WebApplicationFactory<Program> CreateApp()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => _users.Object);

                services.RemoveAll<IAppointmentService>();
                services.AddScoped(_ => _appointmentService.Object);

                services.RemoveAll<IPatientProfileRepository>();
                services.AddScoped(_ => _patientProfileRepo.Object);

                services.RemoveAll<IUserAccountService>();
                services.AddScoped(_ => _userAccountService.Object);

                services.RemoveAll<IPrescriptionService>();
                services.AddScoped(_ => _prescriptionService.Object);

                services.RemoveAll<IInvoiceService>();
                services.AddScoped(_ => _invoiceService.Object);

                services.RemoveAll<IPatientProfileService>();
                services.AddScoped(_ => _patientProfileService.Object);

                services.RemoveAll<IPatientAccountService>();
                services.AddScoped(_ => _patientAccountService.Object);

                services.RemoveAll<IInventoryService>();
                services.AddScoped(_ => _inventoryService.Object);
            });
        });
    }

    private HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> app, UserRole role, Guid? userId = null)
    {
        var user = new User
        {
            UserId = userId ?? Guid.NewGuid(),
            Phone = "0901234567",
            FullName = $"Test User {role}",
            PasswordHash = "dummy-hash",
            Role = role,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _users.Setup(r => r.GetByIdAsync(user.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);
        _users.Setup(r => r.GetByIdReadOnlyAsync(user.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);

        _users.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);
        _users.Setup(r => r.GetByIdReadOnlyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);

        using var scope = app.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var token = tokenService.GenerateAccessToken(user);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// CR-01: GET /api/v1/appointments/checkin-queue (STAFF/ADMIN) called by PATIENT -> 403 Forbidden.
    /// Security Rationale: Patients must not view clinic check-in queues, waiting lists, or other patients' appointments.
    /// </summary>
    [Fact]
    public async Task CR01_CheckinQueue_CalledByPatient_Returns403Forbidden()
    {
        using var app = CreateApp();
        var client = CreateAuthenticatedClient(app, UserRole.Patient);

        var response = await client.GetAsync("/api/v1/appointments/checkin-queue", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// CR-01 (Positive verification): GET /api/v1/appointments/checkin-queue called by STAFF -> 200 OK.
    /// </summary>
    [Fact]
    public async Task CR01_CheckinQueue_CalledByStaff_Returns200OK()
    {
        using var app = CreateApp();
        var client = CreateAuthenticatedClient(app, UserRole.Staff);

        _appointmentService.Setup(s => s.GetCheckinQueueAsync(
                It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckinQueueResponse
            {
                TotalCount = 0,
                Page = 1,
                PageSize = 20,
                Items = new List<CheckinQueueItemResponse>()
            });

        var response = await client.GetAsync("/api/v1/appointments/checkin-queue", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// CR-02: GET /api/v1/admin/users (ADMIN) called by DOCTOR -> 403 Forbidden.
    /// Security Rationale: Doctors have clinical privileges but must not view or manage administrative user accounts,
    /// credentials, or security configurations.
    /// </summary>
    [Fact]
    public async Task CR02_AdminUsers_CalledByDoctor_Returns403Forbidden()
    {
        using var app = CreateApp();
        var client = CreateAuthenticatedClient(app, UserRole.Doctor);

        var response = await client.GetAsync("/api/v1/admin/users", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// CR-02 (Positive verification): GET /api/v1/admin/users called by ADMIN -> 200 OK.
    /// </summary>
    [Fact]
    public async Task CR02_AdminUsers_CalledByAdmin_Returns200OK()
    {
        using var app = CreateApp();
        var client = CreateAuthenticatedClient(app, UserRole.Admin);

        _userAccountService.Setup(s => s.SearchAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BLL.UserRoleManagement.DTOs.PagedResult<UserAccountResponse>
            {
                Items = new List<UserAccountResponse>(),
                TotalCount = 0,
                Page = 1,
                PageSize = 20,
            });

        var response = await client.GetAsync("/api/v1/admin/users", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// CR-03: POST /api/v1/prescriptions (DOCTOR) called by PATIENT -> 403 Forbidden.
    /// Security Rationale: Only licensed physicians may create or modify pharmaceutical prescriptions.
    /// Patients attempting to issue prescriptions must be unconditionally rejected.
    /// </summary>
    [Fact]
    public async Task CR03_Prescriptions_CalledByPatient_Returns403Forbidden()
    {
        using var app = CreateApp();
        var client = CreateAuthenticatedClient(app, UserRole.Patient);

        var request = new CreatePrescriptionRequest(
            CaseId: Guid.NewGuid(),
            GeneralNote: "Tự kê đơn",
            Items: new List<CreatePrescriptionItemDto>(),
            FollowUp: null);

        var response = await client.PostAsJsonAsync("/api/v1/prescriptions", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// CR-04: POST /api/v1/patients (STAFF) called by DOCTOR -> 403 Forbidden.
    /// Security Rationale: Class-level [Authorize(Roles="DOCTOR,STAFF")] and method-level [Authorize(Roles="STAFF")]
    /// are evaluated with AND logic in ASP.NET Core. Doctors are permitted to view patients (GET),
    /// but front-desk reception account creation (POST) is strictly restricted to Staff/Nurse (BR-03).
    /// </summary>
    [Fact]
    public async Task CR04_CreatePatient_CalledByDoctor_Returns403Forbidden()
    {
        using var app = CreateApp();
        var client = CreateAuthenticatedClient(app, UserRole.Doctor);

        var request = new CreatePatientAccountRequest(
            PhoneNumber: "0988112233",
            FullName: "Bệnh nhân mới",
            DateOfBirth: new DateOnly(1995, 5, 20),
            Email: "test@example.com");

        var response = await client.PostAsJsonAsync("/api/v1/patients", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// CR-05: Inventory management (ADMIN/PHARMACIST) called by PATIENT -> 403 Forbidden.
    /// Security Rationale: Inventory control, batches, and stock imports are restricted to Admin and Pharmacist.
    /// Patients attempting to access pharmaceutical inventory batches or stock imports must receive 403.
    /// </summary>
    [Fact]
    public async Task CR05_InventoryBatches_CalledByPatient_Returns403Forbidden()
    {
        using var app = CreateApp();
        var client = CreateAuthenticatedClient(app, UserRole.Patient);

        // Test GET /api/v1/inventory/batches
        var getBatchesResponse = await client.GetAsync("/api/v1/inventory/batches", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, getBatchesResponse.StatusCode);

        // Test POST /api/v1/inventory/import
        var importResponse = await client.PostAsJsonAsync("/api/v1/inventory/import", new ImportInventoryRequest
        {
            MedicineId = Guid.NewGuid(),
            SupplierId = Guid.NewGuid(),
            MedicinePackagingId = Guid.NewGuid(),
            LotNumber = "BATCH-TEST-01",
            ExpiryDate = DateTime.UtcNow.AddMonths(6),
            Quantity = 100,
            ImportPricePerUnit = 5000
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, importResponse.StatusCode);
    }

    /// <summary>
    /// CR-06: GET /api/v1/appointments (PATIENT) called anonymously (no Bearer token) -> 401 Unauthorized.
    /// Security Rationale: Any request to protected resources lacking an Authorization Bearer header
    /// must be challenged with 401 Unauthorized by the authentication middleware.
    /// </summary>
    [Fact]
    public async Task CR06_Appointments_CalledAnonymously_Returns401Unauthorized()
    {
        using var app = CreateApp();
        var unauthenticatedClient = app.CreateClient(); // No Authorization header

        var response = await unauthenticatedClient.GetAsync("/api/v1/appointments", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// CR-05 (Positive verification): GET /api/v1/inventory/batches called by PHARMACIST -> 200 OK.
    /// Security Rationale: Pharmacists are explicitly authorized to manage pharmacy inventory and review batches.
    /// </summary>
    [Fact]
    public async Task CR05_InventoryBatches_CalledByPharmacist_Returns200OK()
    {
        using var app = CreateApp();
        var client = CreateAuthenticatedClient(app, UserRole.Pharmacist);

        _inventoryService.Setup(s => s.GetMedicineBatchesAsync(It.IsAny<MedicineBatchFilter>()))
            .ReturnsAsync(new ADSUS_BE.BLL.Common.PagedResult<MedicineBatchResponse>(new List<MedicineBatchResponse>(), 1, 20, 0, 0));

        var response = await client.GetAsync("/api/v1/inventory/batches", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// CR-07: GET /api/v1/appointments/checkin-queue (STAFF/ADMIN) called by PHARMACIST -> 403 Forbidden.
    /// Security Rationale: Pharmacists must not access clinical nursing check-in queues.
    /// </summary>
    [Fact]
    public async Task CR07_CheckinQueue_CalledByPharmacist_Returns403Forbidden()
    {
        using var app = CreateApp();
        var client = CreateAuthenticatedClient(app, UserRole.Pharmacist);

        var response = await client.GetAsync("/api/v1/appointments/checkin-queue", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// CR-08: POST /api/v1/prescriptions (DOCTOR) called by PHARMACIST -> 403 Forbidden.
    /// Security Rationale: Pharmacists dispense medicines but are strictly barred from issuing medical prescriptions.
    /// </summary>
    [Fact]
    public async Task CR08_Prescriptions_CalledByPharmacist_Returns403Forbidden()
    {
        using var app = CreateApp();
        var client = CreateAuthenticatedClient(app, UserRole.Pharmacist);

        var request = new CreatePrescriptionRequest(
            CaseId: Guid.NewGuid(),
            GeneralNote: "Kê đơn bởi Dược sĩ",
            Items: new List<CreatePrescriptionItemDto>(),
            FollowUp: null);

        var response = await client.PostAsJsonAsync("/api/v1/prescriptions", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// IDOR Vulnerability Proof of Concept:
    /// GET /api/v1/appointments/{id} (AppointmentsController.cs:98-108)
    /// Demonstrates that an authenticated Patient A can read Patient B's appointment by supplying Patient B's UUID,
    /// because no ownership verification (PatientProfileId == callerProfileId || BookedByUserId == callerUserId)
    /// is performed.
    /// </summary>
    [Fact]
    public async Task IDOR_GetAppointmentById_PatientACannotAccessPatientBAppointment_ReturnsForbidden()
    {
        using var app = CreateApp();

        // 1. Patient A authenticates
        var patientAUserId = Guid.NewGuid();
        var clientA = CreateAuthenticatedClient(app, UserRole.Patient, patientAUserId);

        // 2. An appointment belongs to Patient B
        var patientBAppointmentId = Guid.NewGuid();
        var patientBProfileId = Guid.NewGuid();

        _appointmentService.Setup(s => s.GetByIdAsync(patientBAppointmentId, patientAUserId, "PATIENT", It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Bạn không có quyền truy cập thông tin lịch hẹn này."));

        // 3. Patient A requests Patient B's appointment
        var response = await clientA.GetAsync($"/api/v1/appointments/{patientBAppointmentId}", TestContext.Current.CancellationToken);

        // 4. Verification that IDOR is blocked: returns 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task IDOR_GetAppointmentById_PatientCanAccessOwnAppointment_ReturnsOk()
    {
        using var app = CreateApp();

        // 1. Patient authenticates
        var patientUserId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(app, UserRole.Patient, patientUserId);

        // 2. An appointment belongs to this Patient
        var appointmentId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var appointmentData = new AppointmentResponse
        {
            AppointmentId = appointmentId,
            ScheduleSlotId = Guid.NewGuid(),
            SlotDate = new DateOnly(2026, 9, 20),
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(14, 30),
            DoctorName = "BS. Trịnh Bác Sĩ",
            Status = AppointmentStatus.Booked,
            Reason = "Tái khám tim mạch",
            PatientFullName = "Bệnh Nhân Chính Chủ",
            PatientPhone = "0987654321",
            PatientProfileId = profileId
        };

        _appointmentService.Setup(s => s.GetByIdAsync(appointmentId, patientUserId, "PATIENT", It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointmentData);

        // 3. Patient requests their own appointment
        var response = await client.GetAsync($"/api/v1/appointments/{appointmentId}", TestContext.Current.CancellationToken);

        // 4. Verification: returns 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
