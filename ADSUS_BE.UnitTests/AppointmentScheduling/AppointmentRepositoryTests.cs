using System;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ADSUS_BE.UnitTests.AppointmentScheduling;

/// <summary>
/// Kiểm truy vấn thật của AppointmentRepository qua EF Core InMemory, không mock —
/// AppointmentServiceTests mock IAppointmentRepository nên không bắt được lỗi nằm ngay
/// trong câu truy vấn (ví dụ quên join đúng theo doctor/ngày).
/// </summary>
public class AppointmentRepositoryTests
{
    private static AppDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    [Fact]
    public async Task ListByDoctorAsync_FiltersBySlotDoctorAndDateRange_IncludesPatientName()
    {
        await using var db = CreateContext();
        var doctorId = Guid.NewGuid();
        var otherDoctorId = Guid.NewGuid();
        var inRangeDate = new DateOnly(2026, 7, 10);
        var outOfRangeDate = new DateOnly(2026, 7, 20);

        var patientUser = new User
        {
            UserId = Guid.NewGuid(),
            Phone = "0900000001",
            FullName = "Nguyễn Thị Lan",
            PasswordHash = "khong-dung-toi-trong-bai-test-nay",
            Role = UserRole.Patient,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var patientProfile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = patientUser.UserId,
            CreatedBy = doctorId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(patientUser);
        db.PatientProfiles.Add(patientProfile);

        var slotForDoctorInRange = BuildSlot(doctorId, inRangeDate);
        var slotForDoctorOutOfRange = BuildSlot(doctorId, outOfRangeDate);
        var slotForOtherDoctor = BuildSlot(otherDoctorId, inRangeDate);
        db.ScheduleSlots.AddRange(slotForDoctorInRange, slotForDoctorOutOfRange, slotForOtherDoctor);

        db.Appointments.AddRange(
            BuildAppointment(slotForDoctorInRange.SlotId, patientProfile.PatientProfileId, AppointmentStatus.Booked),
            BuildAppointment(slotForDoctorOutOfRange.SlotId, patientProfile.PatientProfileId, AppointmentStatus.Booked),
            BuildAppointment(slotForOtherDoctor.SlotId, patientProfile.PatientProfileId, AppointmentStatus.Booked));
        await db.SaveChangesAsync();

        var sut = new AppointmentRepository(db);
        var result = await sut.ListByDoctorAsync(doctorId, inRangeDate, inRangeDate, CancellationToken.None);

        var appointment = Assert.Single(result);
        Assert.Equal(slotForDoctorInRange.SlotId, appointment.SlotId);
        Assert.Equal("Nguyễn Thị Lan", appointment.PatientProfile.User.FullName);
    }

    private static ScheduleSlot BuildSlot(Guid doctorId, DateOnly date) => new()
    {
        SlotId = Guid.NewGuid(),
        DoctorId = doctorId,
        SlotDate = date,
        StartTime = new TimeOnly(8, 0),
        EndTime = new TimeOnly(8, 30),
        Status = SlotStatus.Booked,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static Appointment BuildAppointment(Guid slotId, Guid patientProfileId, AppointmentStatus status) => new()
    {
        AppointmentId = Guid.NewGuid(),
        SlotId = slotId,
        PatientProfileId = patientProfileId,
        Status = status,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };
}
