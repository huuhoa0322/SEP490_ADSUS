using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.MedicalRecord.Mappers;

/// <summary>
/// Liệt kê tường minh từng field, không generic/reflection — xem chú thích ở PatientProfileMapper.
///
/// Mapper không tự đi ký URL: ký là lời gọi mạng bất đồng bộ. Service ký trước rồi truyền
/// từ điển imageId → url vào đây, nhờ vậy mapper vẫn thuần tuý và đọc là hiểu.
/// </summary>
public static class CaseMapper
{
    public static UltrasoundImageResponse ToImageResponse(
        UltrasoundImage image,
        string? signedUrl) => new(
        ImageId: image.ImageId,
        CaseId: image.CaseId,
        ImageUrl: signedUrl,
        UploadedAt: image.UploadedAt,
        Note: image.Note);

    public static CaseResponse ToStaffResponse(
        Case medicalCase,
        IReadOnlyDictionary<Guid, string?> imageUrls) => new(
        CaseId: medicalCase.CaseId,
        PatientProfileId: medicalCase.PatientProfileId,
        DoctorId: medicalCase.DoctorId,
        DoctorName: medicalCase.Doctor?.FullName ?? string.Empty,
        VisitDate: medicalCase.VisitDate,
        ClinicalInfo: medicalCase.ClinicalInfo,
        Status: medicalCase.Status.ToApiString(),
        FinalDiagnosis: medicalCase.FinalDiagnosis,
        DoctorConclusion: medicalCase.DoctorConclusion,
        PatientProfile: medicalCase.PatientProfile is null
            ? null
            : PatientProfileMapper.ToResponse(medicalCase.PatientProfile),
        UltrasoundImages: medicalCase.UltrasoundImages
            .OrderBy(i => i.UploadedAt)
            .Select(i => ToImageResponse(i, imageUrls.GetValueOrDefault(i.ImageId)))
            .ToList(),
        // AiResults mapping removed
        Prescription: ToPrescriptionSummary(medicalCase),
        CreatedAt: medicalCase.CreatedAt,
        UpdatedAt: medicalCase.UpdatedAt);

    /// <summary>
    /// Bản cho bệnh nhân. So với ToStaffResponse thì thiếu clinicalInfo, patientProfile, aiResults
    /// và các mốc thời gian — đó là chủ đích (GB-05). Ultrasound images ĐƯỢC bao gồm (đính chính
    /// 15/08/2026, xem PatientCaseResponse's doc comment).
    /// </summary>
    public static PatientCaseResponse ToPatientResponse(
        Case medicalCase,
        IReadOnlyDictionary<Guid, string?> imageUrls) => new(
        CaseId: medicalCase.CaseId,
        DoctorId: medicalCase.DoctorId,
        DoctorName: medicalCase.Doctor?.FullName ?? string.Empty,
        VisitDate: medicalCase.VisitDate,
        Status: medicalCase.Status.ToApiString(),
        FinalDiagnosis: medicalCase.FinalDiagnosis,
        DoctorConclusion: medicalCase.DoctorConclusion,
        Prescription: ToPrescriptionSummary(medicalCase),
        UltrasoundImages: medicalCase.UltrasoundImages
            .OrderBy(i => i.UploadedAt)
            .Select(i => ToImageResponse(i, imageUrls.GetValueOrDefault(i.ImageId)))
            .ToList());

    public static CaseSummaryResponse ToSummary(Case medicalCase) => new(
        CaseId: medicalCase.CaseId,
        VisitDate: medicalCase.VisitDate,
        Status: medicalCase.Status.ToApiString(),
        DoctorId: medicalCase.DoctorId);

    public static StaffCaseSummaryResponse ToStaffSummary(Case medicalCase) => new(
        CaseId: medicalCase.CaseId,
        VisitDate: medicalCase.VisitDate,
        Status: medicalCase.Status.ToApiString(),
        DoctorId: medicalCase.DoctorId,
        CreatedAt: medicalCase.CreatedAt);

    /// <summary>
    /// Đơn thuốc được coi là "hiện hành" cho một ca khám — cùng ngày kê thì phân định bằng
    /// CreatedAt, để #23 (GET /cases/{id}) và #27 (PDF) không bao giờ chọn khác nhau.
    /// Dùng chung ở cả hai nơi thay vì mỗi nơi tự viết lại logic sắp xếp — CaseReportService
    /// từng có một bản sao chép thiếu ThenByDescending, khiến PDF và API có thể trả về hai
    /// đơn thuốc khác nhau cho cùng một ca có hai đơn cùng ngày.
    /// </summary>
    public static Prescription? SelectLatestPrescription(Case medicalCase) =>
        medicalCase.Prescriptions
            .OrderByDescending(p => p.PrescribedDate)
            .ThenByDescending(p => p.CreatedAt)
            .FirstOrDefault();

    private static PrescriptionSummary? ToPrescriptionSummary(Case medicalCase)
    {
        var prescription = SelectLatestPrescription(medicalCase);

        return prescription is null
            ? null
            : new PrescriptionSummary(
                PrescriptionId: prescription.PrescriptionId,
                Status: prescription.Status.ToApiString());
    }
}
