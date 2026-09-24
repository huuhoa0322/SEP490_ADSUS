using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.MedicalRecord.Interfaces;

public interface ICaseService
{
    Task<IReadOnlyList<UltrasoundImageResponse>> ListImagesAsync(
        Guid caseId, CancellationToken ct = default);

    /// <summary>
    /// #23 cho Bác sĩ/Điều dưỡng — bản đầy đủ.
    /// Cho phép xem ca ở mọi trạng thái (kể cả BOOKED trước check-in).
    /// </summary>
    Task<CaseResponse> GetForStaffAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>
    /// #23 cho Bệnh nhân — chỉ ca của chính họ và chỉ khi đã CONFIRMED.
    /// Không thoả điều kiện thì ném ResourceNotFoundException (404), KHÔNG phải 403.
    /// </summary>
    Task<PatientCaseResponse> GetForPatientAsync(
        Guid caseId, Guid callerUserId, CancellationToken ct = default);

    /// <summary>#24 — cho Bác sĩ/Điều dưỡng (Web SCR-12). Có CreatedAt, xem StaffCaseSummaryResponse.</summary>
    Task<PagedResult<StaffCaseSummaryResponse>> ListByPatientProfileAsync(
        Guid patientProfileId,
        string? status,
        string sortOrder,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>#25 — danh sách lần khám của chính người gọi, luôn ép về CONFIRMED.</summary>
    Task<PagedResult<CaseSummaryResponse>> ListMineAsync(
        Guid callerUserId, int page, int pageSize, CancellationToken ct = default);

    Task<CaseResponse> CreateAsync(
        CreateCaseRequest request, CancellationToken ct = default);

    /// <summary>
    /// Thêm 07/08/2026 — "Lưu kết luận". Chỉ lưu nội dung, KHÔNG đổi trạng thái ca — sửa lại
    /// được nhiều lần. Cùng hai điều kiện với ConfirmAsync: chỉ Bác sĩ phụ trách CA NÀY (GB-04),
    /// và ca chưa CONFIRMED (GB-01/P2 — ca đã khoá thì không sửa được nữa, kể cả chỉ lưu nháp).
    /// </summary>
    Task<CaseResponse> SaveConclusionAsync(
        Guid caseId, Guid actingDoctorId, CaseConclusionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Thêm 07/08/2026 — "Kết thúc ca khám". Lưu VÀ khoá ca (CONFIRMED) trong cùng một lần gọi.
    /// Chỉ đúng Bác sĩ phụ trách của ca này mới làm được (GB-04), và chỉ làm được MỘT LẦN — ca
    /// đã CONFIRMED thì từ chối luôn (GB-01/P2, không có đường lùi).
    /// </summary>
    Task<CaseResponse> ConfirmAsync(
        Guid caseId, Guid actingDoctorId, CaseConclusionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Chuyển thẳng ca từ CONFIRMED sang END đối với những ca không kê đơn thuốc.
    /// </summary>
    Task<CaseResponse> EndWithoutPrescriptionAsync(
        Guid caseId, Guid actingDoctorId, CancellationToken ct = default);

    /// <summary>
    /// Chuyển Case sang InProgress khi lịch hẹn của nó được check-in (chỉ khi Case đang Booked).
    /// KHÔNG lưu: AppointmentService gọi SaveChanges MỘT lần cho cả lịch hẹn lẫn Case (cùng
    /// DbContext theo request), để hai trạng thái luôn được lưu cùng lúc hoặc không lưu gì.
    /// </summary>
    Task StageCheckinFromAppointmentAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>
    /// Chuyển Case sang Cancelled khi lịch hẹn của nó bị huỷ. KHÔNG lưu — xem
    /// <see cref="StageCheckinFromAppointmentAsync"/>.
    /// </summary>
    Task StageCancelFromAppointmentAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>
    /// Thay toàn bộ triệu chứng của Case khi bệnh nhân sửa thông tin lịch hẹn. <paramref name="symptoms"/>
    /// null nghĩa là không đụng tới triệu chứng (chỉ cập nhật UpdatedAt). KHÔNG lưu — xem
    /// <see cref="StageCheckinFromAppointmentAsync"/>.
    /// </summary>
    Task StageReplaceSymptomsFromAppointmentAsync(
        Guid caseId, IReadOnlyList<SymptomInput>? symptoms, CancellationToken ct = default);

    /// <summary>
    /// Cập nhật Case khi lịch hẹn của nó được đổi sang slot khác. <paramref name="reassignDoctorTo"/>
    /// khác null thì chuyển bác sĩ phụ trách; <paramref name="newStatus"/> khác null thì đặt trạng
    /// thái mới (kèm UpdatedAt). Quyết định giá trị nào thuộc về AppointmentService (tuỳ tình huống
    /// đổi lịch). KHÔNG lưu — xem <see cref="StageCheckinFromAppointmentAsync"/>.
    /// </summary>
    Task StageRescheduleFromAppointmentAsync(
        Guid caseId, Guid? reassignDoctorTo, CaseStatus? newStatus, CancellationToken ct = default);

    /// <summary>Triệu chứng của Case (để trả kèm thông tin lịch hẹn). Case không tồn tại → danh sách rỗng.</summary>
    Task<IReadOnlyList<CaseSymptomResponse>> ListSymptomsAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>
    /// Bác sĩ phụ trách + trạng thái hiện tại của Case, null nếu không có — cho module khác kiểm
    /// tra quyền/trạng thái trước khi thao tác trên ca (vd gắn/gỡ dịch vụ). Nếu Case đang được
    /// track trong request (vừa đổi trạng thái, chưa lưu) thì trả trạng thái đang track đó.
    /// </summary>
    Task<CaseOwnershipInfo?> FindOwnershipAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>Ca đã có ít nhất một ảnh siêu âm chưa.</summary>
    Task<bool> HasUltrasoundImagesAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>
    /// Tạo case từ việc đặt lịch khám (Mobile).
    /// Status của case = BOOKED. Không tạo ảnh, không gửi notification.
    /// </summary>
    Task<Guid> CreateFromBookingAsync(
        Guid patientProfileId,
        Guid doctorId,
        DateOnly visitDate,
        IReadOnlyList<SymptomInput>? symptoms,
        CancellationToken ct = default);

    Task<CaseResponse> UpdateSymptomsAsync(
        Guid caseId, Guid actingDoctorId, UpdateCaseSymptomsRequest request, CancellationToken ct = default);

    Task<CaseResponse> UpdateDiseasesAsync(
        Guid caseId, Guid actingDoctorId, UpdateCaseDiseasesRequest request, CancellationToken ct = default);

    Task<CaseResponse> UpdateAllergiesAsync(
        Guid caseId, Guid actingDoctorId, UpdateCaseAllergiesRequest request, CancellationToken ct = default);

    Task<CaseResponse> UpdateDiagnosesAsync(
        Guid caseId, Guid actingDoctorId, UpdateCaseDiagnosesRequest request, CancellationToken ct = default);
}
