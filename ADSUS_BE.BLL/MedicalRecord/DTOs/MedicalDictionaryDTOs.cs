namespace ADSUS_BE.BLL.MedicalRecord.DTOs;

// Field "Id" (không phải "DiseaseId"/"AllergyTypeId") — khớp đúng contract JSON hiện tại
// (adsus-fe MedicalDisease/MedicalAllergyType đọc `.id`), giữ nguyên như raw entity trước khi
// tách Service, tránh phá vỡ FE đang gọi endpoint này thật.
public sealed record MedicalDiseaseResponse(Guid Id, string Name, bool RequiresNote, bool IsOther);

public sealed record MedicalAllergyTypeResponse(Guid Id, string Name, bool IsOther);
