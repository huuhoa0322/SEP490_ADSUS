using ADSUS_BE.BLL.MedicalRecord.DTOs;

namespace ADSUS_BE.BLL.MedicalRecord.Interfaces;

public interface ISymptomService
{
    Task<IReadOnlyList<SymptomCategoryResponse>> GetCategoriesAsync(CancellationToken ct = default);
}
