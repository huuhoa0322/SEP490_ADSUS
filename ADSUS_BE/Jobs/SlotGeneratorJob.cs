using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Npgsql;
using Quartz;

/// <summary>
/// JOB-02 — Tự sinh slot mỗi ngày.
/// Chạy lúc 00:05 sáng hàng ngày.
/// Sinh slot cho 14 ngày (hôm nay → 13 ngày tới = 2 tuần T2-CN).
/// Chia ca 30 phút: 8h-12h (8 ca), 13h-17h (8 ca) = 16 ca/ngày.
/// </summary>
[DisallowConcurrentExecution]
public sealed class SlotGeneratorJob : IJob
{
    /// <summary>16 ca 30 phút mỗi ngày: 8h-12h (8 ca) + 13h-17h (8 ca).</summary>
    private static readonly (TimeOnly Start, TimeOnly End)[] DailySlots30Min =
    {
        // Buổi sáng
        (new TimeOnly(8, 0),  new TimeOnly(8, 30)),
        (new TimeOnly(8, 30), new TimeOnly(9, 0)),
        (new TimeOnly(9, 0),  new TimeOnly(9, 30)),
        (new TimeOnly(9, 30), new TimeOnly(10, 0)),
        (new TimeOnly(10, 0), new TimeOnly(10, 30)),
        (new TimeOnly(10, 30),new TimeOnly(11, 0)),
        (new TimeOnly(11, 0), new TimeOnly(11, 30)),
        (new TimeOnly(11, 30),new TimeOnly(12, 0)),
        // Buổi chiều
        (new TimeOnly(13, 0), new TimeOnly(13, 30)),
        (new TimeOnly(13, 30),new TimeOnly(14, 0)),
        (new TimeOnly(14, 0), new TimeOnly(14, 30)),
        (new TimeOnly(14, 30),new TimeOnly(15, 0)),
        (new TimeOnly(15, 0), new TimeOnly(15, 30)),
        (new TimeOnly(15, 30),new TimeOnly(16, 0)),
        (new TimeOnly(16, 0), new TimeOnly(16, 30)),
        (new TimeOnly(16, 30),new TimeOnly(17, 0)),
    };

    private readonly IUserRepository _userRepo;
    private readonly IScheduleSlotRepository _slotRepo;
    private readonly ILogger<SlotGeneratorJob> _logger;

    public SlotGeneratorJob(
        IUserRepository userRepo,
        IScheduleSlotRepository slotRepo,
        ILogger<SlotGeneratorJob> logger)
    {
        _userRepo = userRepo;
        _slotRepo = slotRepo;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("[JOB-02] Slot generator started at {Time}", DateTime.UtcNow);

        try
        {
            // 1. Lấy danh sách Doctor Active
            var doctors = await _userRepo.ListActiveDoctorsAsync(context.CancellationToken);
            _logger.LogInformation("[JOB-02] Found {Count} active doctors", doctors.Count);

            // 2. Tính ngày: hôm nay → 13 ngày tới = 14 ngày (2 tuần)
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var endDate = today.AddDays(13); // Hôm nay + 13 = 14 ngày
            _logger.LogInformation("[JOB-02] Generating slots from {From} to {To}", today, endDate);

            // 3. Với mỗi Doctor, sinh slot cho 14 ngày
            var totalSlotsCreated = 0;

            foreach (var doctor in doctors)
            {
                _logger.LogInformation("[JOB-02] Processing doctor {DoctorId} ({Name})",
                    doctor.UserId, doctor.FullName);

                var count = await GenerateSlotsForDoctorAsync(
                    doctor.UserId, today, endDate, context.CancellationToken);
                totalSlotsCreated += count;

                _logger.LogInformation("[JOB-02] Doctor {DoctorId}: created {Count} slots",
                    doctor.UserId, count);
            }

            _logger.LogInformation(
                "[JOB-02] Slot generator completed. Created {Count} slots for {Doctors} doctors",
                totalSlotsCreated, doctors.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[JOB-02] Slot generator failed");
            throw;
        }
    }

    private async Task<int> GenerateSlotsForDoctorAsync(
        Guid doctorId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct)
    {
        var createdCount = 0;
        var skippedPastCount = 0;
        var skippedOverlapCount = 0;
        var now = DateTime.UtcNow;

        // Duyệt qua từng ngày (T2-CN)
        for (var day = fromDate; day <= toDate; day = day.AddDays(1))
        {
            _logger.LogDebug("[JOB-02] Processing day {Date} ({DayOfWeek})", day, day.DayOfWeek);

            var dayCreatedCount = 0;

            // Duyệt qua 16 ca 30 phút
            foreach (var (start, end) in DailySlots30Min)
            {
                // Skip ca đã qua
                var slotDateTime = day.ToDateTime(start, DateTimeKind.Utc);
                if (slotDateTime <= now)
                {
                    skippedPastCount++;
                    continue;
                }

                // Check overlap
                var hasOverlap = await _slotRepo.HasOverlapAsync(
                    doctorId, day, start, end, excludeSlotId: null, ct);
                if (hasOverlap)
                {
                    skippedOverlapCount++;
                    _logger.LogDebug(
                        "[JOB-02] Skipped (overlap): Doctor={DoctorId}, Date={Date}, {Start}-{End}",
                        doctorId, day, start, end);
                    continue;
                }

                // Tạo slot mới
                var slot = new ScheduleSlot
                {
                    SlotId = Guid.NewGuid(),
                    DoctorId = doctorId,
                    SlotDate = day,
                    StartTime = start,
                    EndTime = end,
                    Status = SlotStatus.Open,
                    CreatedAt = now,
                    UpdatedAt = now,
                };

                try
                {
                    await _slotRepo.AddAsync(slot, ct);
                    createdCount++;
                    dayCreatedCount++;
                }
                catch (Exception ex) when (IsDuplicateKey(ex))
                {
                    // Slot đã tồn tại (idempotent)
                    skippedOverlapCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[JOB-02] Failed to create slot: Doctor={DoctorId}, Date={Date}, {Start}-{End}",
                        doctorId, day, start, end);
                }
            }

            _logger.LogDebug(
                "[JOB-02] Day {Date}: created {Created} slots",
                day, dayCreatedCount);
        }

        _logger.LogInformation(
            "[JOB-02] Doctor {DoctorId} summary: created={Created}, skipped_past={Past}, skipped_overlap={Overlap}",
            doctorId, createdCount, skippedPastCount, skippedOverlapCount);

        return createdCount;
    }

    private static bool IsDuplicateKey(Exception ex)
    {
        // PostgreSQL unique constraint violation = 23505
        // PostgreSQL exclusion constraint violation = 23P01
        return ex.InnerException is PostgresException pgEx
            && (pgEx.SqlState == "23505" || pgEx.SqlState == "23P01");
    }
}
