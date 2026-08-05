using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Services;

/// <summary>
/// Sinh danh sách scheduled_time UTC cho MedicationIntakeLog từ 1 PrescriptionItem.
/// Giờ nhắc mặc định khi bệnh nhân chưa có PatientReminderPreference:
///   MORNING = 07:00, NOON = 12:00, EVENING = 20:00.
/// </summary>
public sealed class MedicationIntakeScheduleGenerator : IMedicationIntakeScheduleGenerator
{
    // Default reminder times when patient has not set preferences.
    private static readonly TimeOnly DefaultMorning = new(7, 0);
    private static readonly TimeOnly DefaultMidday  = new(12, 0);
    private static readonly TimeOnly DefaultEvening  = new(20, 0);

    public Task<IReadOnlyList<ScheduledDose>> GenerateAsync(
        PrescriptionItemWithPatient item,
        IReadOnlyList<ScheduleSlot> slots,
        TimeOnly patientMorningTime,
        TimeOnly patientMiddayTime,
        TimeOnly patientEveningTime,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var morningTime = patientMorningTime == default ? DefaultMorning : patientMorningTime;
        var middayTime  = patientMiddayTime  == default ? DefaultMidday  : patientMiddayTime;
        var eveningTime = patientEveningTime  == default ? DefaultEvening : patientEveningTime;

        var result = new List<ScheduledDose>();

        for (var dayOffset = 0; dayOffset < item.DurationDays; dayOffset++)
        {
            var date = item.StartDate.AddDays(dayOffset);

            foreach (var slot in slots)
            {
                var timeOfDay = slot switch
                {
                    ScheduleSlot.Morning => morningTime,
                    ScheduleSlot.Noon    => middayTime,
                    ScheduleSlot.Evening => eveningTime,
                    _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown slot.")
                };

                var scheduledUtc = date.ToDateTime(timeOfDay, DateTimeKind.Utc);

                result.Add(new ScheduledDose(item.PrescriptionItemId, scheduledUtc));
            }
        }

        return Task.FromResult<IReadOnlyList<ScheduledDose>>(result);
    }
}