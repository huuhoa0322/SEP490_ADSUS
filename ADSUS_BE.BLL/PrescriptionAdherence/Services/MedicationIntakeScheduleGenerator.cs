using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.DAL.Data;

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
        DateTime utcNow,
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

                // timeOfDay là giờ sinh hoạt bệnh nhân VN. Đổi sang UTC thật trước khi lưu DB
                // (trừ ClinicClock.Offset = +07:00). Dùng helper có sẵn trong DAL để tránh phụ
                // thuộc TimeZoneInfo của OS (Linux vs Windows đặt tên khác nhau).
                var naiveLocal = date.ToDateTime(timeOfDay, DateTimeKind.Unspecified);
                var scheduledUtc = DateTime.SpecifyKind(naiveLocal - ClinicClock.Offset, DateTimeKind.Utc);

                // Skip doses that are already past on dayOffset=0 (scheduled <= utcNow).
                // Fixes UC-18: doctor prescribes at 15:00 ICT → morning/noon slots for today
                // are already past and must not be generated (avoids OVERTIME status confusion).
                if (dayOffset == 0 && scheduledUtc <= utcNow)
                    continue;

                result.Add(new ScheduledDose(item.PrescriptionItemId, scheduledUtc));
            }
        }

        return Task.FromResult<IReadOnlyList<ScheduledDose>>(result);
    }
}