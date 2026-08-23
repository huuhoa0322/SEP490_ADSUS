using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

public interface ISymptomCategoryRepository
{
    Task<IReadOnlyList<SymptomCategory>> GetAllWithSymptomsAsync(CancellationToken ct = default);
}
