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
        Status: medicalCase.Status.ToString().ToUpperInvariant(),
        FinalDiagnosis: medicalCase.FinalDiagnosis,
        DoctorConclusion: medicalCase.DoctorConclusion,
        PatientProfile: medicalCase.PatientProfile is null
            ? null
            : PatientProfileMapper.ToResponse(medicalCase.PatientProfile),
        UltrasoundImages: medicalCase.UltrasoundImages
            .OrderBy(i => i.UploadedAt)
            .Select(i => ToImageResponse(i, imageUrls.GetValueOrDefault(i.ImageId)))
            .ToList(),
        AiResults: medicalCase.AiResults
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new AiResultSummary(
                AiResultId: r.AiResultId,
                Status: r.Status.ToString().ToUpperInvariant(),
                FindingCount: r.AiFindings.Count))
            .ToList(),
        Prescription: ToPrescriptionSummary(medicalCase),
        CreatedAt: medicalCase.CreatedAt,
        UpdatedAt: medicalCase.UpdatedAt);

    /// <summary>
    /// Bản cho bệnh nhân. So với ToStaffResponse thì thiếu hẳn clinicalInfo, patientProfile,
    /// ultrasoundImages, aiResults và các mốc thời gian — đó là chủ đích (GB-05).
    /// </summary>
    public static PatientCaseResponse ToPatientResponse(Case medicalCase) => new(
        CaseId: medicalCase.CaseId,
        DoctorId: medicalCase.DoctorId,
        DoctorName: medicalCase.Doctor?.FullName ?? string.Empty,
        VisitDate: medicalCase.VisitDate,
        Status: medicalCase.Status.ToString().ToUpperInvariant(),
        FinalDiagnosis: medicalCase.FinalDiagnosis,
        DoctorConclusion: medicalCase.DoctorConclusion,
        Prescription: ToPrescriptionSummary(medicalCase));

    public static CaseSummaryResponse ToSummary(Case medicalCase) => new(
        CaseId: medicalCase.CaseId,
        VisitDate: medicalCase.VisitDate,
        Status: medicalCase.Status.ToString().ToUpperInvariant(),
        DoctorId: medicalCase.DoctorId);

    private static PrescriptionSummary? ToPrescriptionSummary(Case medicalCase)
    {
        var prescription = medicalCase.Prescriptions
            .OrderByDescending(p => p.PrescribedDate)
            .FirstOrDefault();

        return prescription is null
            ? null
            : new PrescriptionSummary(
                PrescriptionId: prescription.PrescriptionId,
                Status: prescription.Status.ToString().ToUpperInvariant());
    }
}
