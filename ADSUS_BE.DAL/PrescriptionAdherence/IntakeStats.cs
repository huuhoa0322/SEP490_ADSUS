namespace ADSUS_BE.DAL.PrescriptionAdherence;

public sealed record IntakeStats(
    Guid PrescriptionItemId,
    int TotalDoses,
    int TakenDoses,
    int PendingDoses,
    double AdherencePercent);
