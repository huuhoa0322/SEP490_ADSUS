using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.DAL.Repositories.Interfaces;

namespace ADSUS_BE.BLL.MedicalRecord.Services;

public sealed class MedicalDictionaryService : IMedicalDictionaryService
{
    private readonly IMedicalDictionaryRepository _repository;

    public MedicalDictionaryService(IMedicalDictionaryRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<MedicalDiseaseResponse>> GetDiseasesAsync(CancellationToken ct = default)
    {
        var diseases = await _repository.ListDiseasesAsync(ct);

        return diseases
            .Select(d => new MedicalDiseaseResponse(d.Id, d.Name, d.RequiresNote, d.IsOther))
            .ToList();
    }

    public async Task<IReadOnlyList<MedicalAllergyTypeResponse>> GetAllergyTypesAsync(CancellationToken ct = default)
    {
        var allergyTypes = await _repository.ListAllergyTypesAsync(ct);

        return allergyTypes
            .Select(a => new MedicalAllergyTypeResponse(a.Id, a.Name, a.IsOther))
            .ToList();
    }
}
