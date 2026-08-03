namespace ADSUS_BE.BLL.AppointmentScheduling.Exceptions;

public sealed class ScheduleSlotNotFoundException : Exception
{
    public Guid Id { get; }
    public ScheduleSlotNotFoundException(Guid id)
        : base($"Không tìm thấy khung giờ khám {id}.")
    {
        Id = id;
    }
}

public sealed class DoctorNotFoundException : Exception
{
    public Guid DoctorId { get; }
    public DoctorNotFoundException(Guid doctorId)
        : base($"Không tìm thấy bác sĩ {doctorId}.")
    {
        DoctorId = doctorId;
    }
}

public sealed class SlotOverlapException : Exception
{
    public SlotOverlapException()
        : base("Khung giờ này bị chồng lấn với 1 khung giờ đã có (UC-15 BR-01).")
    {
    }
}

public sealed class SlotAlreadyClosedException : Exception
{
    public SlotAlreadyClosedException()
        : base("Khung giờ đã ở trạng thái CLOSED — không thể đóng lại (UC-15 BR-02).")
    {
    }
}