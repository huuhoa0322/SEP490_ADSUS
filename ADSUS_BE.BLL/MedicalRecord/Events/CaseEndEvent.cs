using ADSUS_BE.BLL.Common.Events;

namespace ADSUS_BE.BLL.MedicalRecord.Events;

/// <summary>
/// Domain event raised when a medical case is marked as END.
/// This triggers downstream actions like canceling related appointments.
/// </summary>
public record CaseEndEvent(
    /// <summary>
    /// The ID of the case that ended.
    /// </summary>
    Guid CaseId,

    /// <summary>
    /// The patient profile ID associated with this case.
    /// </summary>
    Guid PatientProfileId,

    /// <summary>
    /// The doctor who ended the case.
    /// </summary>
    Guid? DoctorId,

    /// <summary>
    /// Optional notes/reason for ending the case.
    /// </summary>
    string? Notes,

    /// <summary>
    /// When the event occurred.
    /// </summary>
    DateTime OccurredAt,

    /// <summary>
    /// Unique identifier for this event.
    /// </summary>
    Guid EventId
) : IDomainEvent
{
    /// <summary>
    /// Factory method to create a new CaseEndEvent.
    /// </summary>
    public static CaseEndEvent Create(Guid caseId, Guid patientProfileId, Guid? doctorId = null, string? notes = null)
        => new(
            CaseId: caseId,
            PatientProfileId: patientProfileId,
            DoctorId: doctorId,
            Notes: notes,
            OccurredAt: DateTime.UtcNow,
            EventId: Guid.NewGuid()
        );
}
