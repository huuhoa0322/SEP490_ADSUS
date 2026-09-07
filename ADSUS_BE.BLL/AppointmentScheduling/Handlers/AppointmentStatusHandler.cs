using ADSUS_BE.BLL.Common.Events;
using ADSUS_BE.BLL.MedicalRecord.Events;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.AppointmentScheduling.Handlers;

/// <summary>
/// Handles CaseEndEvent by marking all related appointments as COMPLETED.
/// When a case ends (without prescription), the related appointment that was checked-in
/// (Approved) is marked as Completed.
/// </summary>
public class AppointmentStatusHandler : IEventHandler<CaseEndEvent>
{
    private readonly IAppointmentRepository _appointmentRepo;
    private readonly ILogger<AppointmentStatusHandler> _logger;

    public AppointmentStatusHandler(
        IAppointmentRepository appointmentRepo,
        ILogger<AppointmentStatusHandler> logger)
    {
        _appointmentRepo = appointmentRepo;
        _logger = logger;
    }

    public async Task HandleAsync(CaseEndEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[AppointmentStatusHandler] Processing CaseEndEvent for CaseId: {CaseId}",
            @event.CaseId);

        await _appointmentRepo.CancelByCaseAsync(@event.CaseId, ct);

        _logger.LogInformation(
            "[AppointmentStatusHandler] Completed appointments for CaseId: {CaseId}",
            @event.CaseId);
    }
}
