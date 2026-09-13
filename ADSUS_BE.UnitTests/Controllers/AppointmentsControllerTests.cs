using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.Controllers;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.Controllers;

/// <summary>
/// Unit tests for AppointmentsController, specifically BookForPatient (Staff booking).
/// </summary>
public class AppointmentsControllerTests
{
    private readonly Mock<IAppointmentService> _appointmentService = new();
    private readonly Mock<IPatientProfileRepository> _patientProfileRepo = new();
    private readonly AppointmentsController _sut;

    public AppointmentsControllerTests()
    {
        _sut = new AppointmentsController(
            _appointmentService.Object,
            _patientProfileRepo.Object);
    }

    [Fact]
    public async Task BookForPatient_ValidRequest_Returns201Created()
    {
        // Arrange
        var patientProfileId = Guid.NewGuid();
        var scheduleSlotId = Guid.NewGuid();
        var request = new StaffBookAppointmentRequest
        {
            PatientProfileId = patientProfileId,
            ScheduleSlotId = scheduleSlotId,
            Reason = "Tái khám định kỳ",
        };

        var profile = new PatientProfile
        {
            PatientProfileId = patientProfileId,
            UserId = Guid.NewGuid(),
        };

        var expectedAppointment = new AppointmentResponse
        {
            AppointmentId = Guid.NewGuid(),
            ScheduleSlotId = scheduleSlotId,
            DoctorName = "BS. Nguyễn Văn A",
            SlotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Status = AppointmentStatus.Booked,
            Reason = request.Reason,
            CaseId = Guid.NewGuid(),
        };

        _patientProfileRepo.Setup(r => r.GetByIdAsync(patientProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _appointmentService.Setup(s => s.BookAppointmentAsync(
            patientProfileId,
            It.Is<BookAppointmentRequest>(b => b.ScheduleSlotId == scheduleSlotId && b.Reason == request.Reason),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAppointment);

        // Act
        var result = await _sut.BookForPatient(request, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);

        var apiResponse = Assert.IsType<ApiResponse<AppointmentResponse>>(objectResult.Value);
        Assert.Equal(201, apiResponse.Code);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(expectedAppointment.AppointmentId, apiResponse.Data.AppointmentId);
        Assert.Equal(expectedAppointment.CaseId, apiResponse.Data.CaseId);
    }

    [Fact]
    public async Task BookForPatient_EmptyPatientProfileId_Returns400BadRequest()
    {
        // Arrange
        var request = new StaffBookAppointmentRequest
        {
            PatientProfileId = Guid.Empty,
            ScheduleSlotId = Guid.NewGuid(),
        };

        // Act
        var result = await _sut.BookForPatient(request, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);

        var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.Equal(400, apiResponse.Code);
        Assert.Contains("không được để trống", apiResponse.Message);
        _appointmentService.Verify(s => s.BookAppointmentAsync(It.IsAny<Guid>(), It.IsAny<BookAppointmentRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BookForPatient_EmptyScheduleSlotId_Returns400BadRequest()
    {
        // Arrange
        var request = new StaffBookAppointmentRequest
        {
            PatientProfileId = Guid.NewGuid(),
            ScheduleSlotId = Guid.Empty,
        };

        // Act
        var result = await _sut.BookForPatient(request, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);

        var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.Equal(400, apiResponse.Code);
        Assert.Contains("không được để trống", apiResponse.Message);
        _appointmentService.Verify(s => s.BookAppointmentAsync(It.IsAny<Guid>(), It.IsAny<BookAppointmentRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BookForPatient_ProfileNotFound_Returns404NotFound()
    {
        // Arrange
        var patientProfileId = Guid.NewGuid();
        var request = new StaffBookAppointmentRequest
        {
            PatientProfileId = patientProfileId,
            ScheduleSlotId = Guid.NewGuid(),
        };

        _patientProfileRepo.Setup(r => r.GetByIdAsync(patientProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PatientProfile?)null);

        // Act
        var result = await _sut.BookForPatient(request, CancellationToken.None);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);

        var apiResponse = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
        Assert.Equal(404, apiResponse.Code);
        Assert.Contains("Không tìm thấy hồ sơ bệnh nhân", apiResponse.Message);
        _appointmentService.Verify(s => s.BookAppointmentAsync(It.IsAny<Guid>(), It.IsAny<BookAppointmentRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BookForPatient_ServiceThrowsInvalidOperationException_Returns400BadRequest()
    {
        // Arrange
        var patientProfileId = Guid.NewGuid();
        var scheduleSlotId = Guid.NewGuid();
        var request = new StaffBookAppointmentRequest
        {
            PatientProfileId = patientProfileId,
            ScheduleSlotId = scheduleSlotId,
        };

        var profile = new PatientProfile
        {
            PatientProfileId = patientProfileId,
            UserId = Guid.NewGuid(),
        };

        _patientProfileRepo.Setup(r => r.GetByIdAsync(patientProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _appointmentService.Setup(s => s.BookAppointmentAsync(
            patientProfileId,
            It.IsAny<BookAppointmentRequest>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Bạn đã có 3 lịch hẹn đang chờ."));

        // Act
        var result = await _sut.BookForPatient(request, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);

        var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.Equal(400, apiResponse.Code);
        Assert.Equal("Bạn đã có 3 lịch hẹn đang chờ.", apiResponse.Message);
    }
}
