namespace ADSUS_BE.BLL.PrescriptionAdherence.Exceptions;

/// <summary>Module 7 UC-11: truy vấn đơn thuốc không tồn tại.</summary>
public sealed class PrescriptionNotFoundException : Exception
{
    public Guid PrescriptionId { get; }

    public PrescriptionNotFoundException(Guid id)
        : base($"Không tìm thấy đơn thuốc với mã {id}.")
    {
        PrescriptionId = id;
    }
}

/// <summary>Module 7 UC-18 BR-04: ca khám chưa Confirmed.</summary>
public sealed class CaseNotConfirmedException : Exception
{
    public Guid CaseId { get; }

    public CaseNotConfirmedException(Guid caseId)
        : base($"Ca khám {caseId} chưa được bác sĩ duyệt (chưa ở trạng thái CONFIRMED).")
    {
        CaseId = caseId;
    }
}

/// <summary>Module 7 UC-18 BR-03: ca khám đã có đơn ACTIVE — không cho kê thêm.</summary>
public sealed class ActivePrescriptionExistsException : Exception
{
    public Guid CaseId { get; }

    public ActivePrescriptionExistsException(Guid caseId)
        : base($"Ca khám {caseId} đã có đơn thuốc đang ACTIVE — không thể kê thêm.")
    {
        CaseId = caseId;
    }
}

/// <summary>Module 7 UC-18: ca khám không tồn tại.</summary>
public sealed class CaseNotFoundException : Exception
{
    public Guid CaseId { get; }

    public CaseNotFoundException(Guid caseId)
        : base($"Không tìm thấy ca khám {caseId}.")
    {
        CaseId = caseId;
    }
}

/// <summary>Module 7 UC-18: bác sĩ từ JWT không hợp lệ.</summary>
public sealed class DoctorNotFoundException : Exception
{
    public DoctorNotFoundException()
        : base("Không xác định được bác sĩ từ phiên đăng nhập.")
    {
    }
}

/// <summary>Module 7 UC-18: gọi endpoint không đúng vai trò (vd: Patient gọi POST).</summary>
public sealed class DoctorOnlyActionException : Exception
{
    public DoctorOnlyActionException(string action)
        : base($"Hành động '{action}' chỉ dành cho bác sĩ.")
    {
    }
}
