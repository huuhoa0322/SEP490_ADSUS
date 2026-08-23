using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.DAL.Repositories.Interfaces;

namespace ADSUS_BE.BLL.MedicalRecord.Services;

public sealed class SymptomService : ISymptomService
{
    private readonly ISymptomCategoryRepository _categories;

    public SymptomService(ISymptomCategoryRepository categories)
    {
        _categories = categories;
    }

    public async Task<IReadOnlyList<SymptomCategoryResponse>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var categories = await _categories.GetAllWithSymptomsAsync(ct);

        return categories.Select(c => new SymptomCategoryResponse(
            CategoryId: c.CategoryId,
            Name: c.Name,
            IsOther: c.IsOther,
            Symptoms: c.Symptoms.Select(s => new SymptomItemResponse(
                SymptomId: s.SymptomId,
                Name: s.Name,
                IsOther: s.IsOther)).ToList()
        )).ToList();
    }
}
