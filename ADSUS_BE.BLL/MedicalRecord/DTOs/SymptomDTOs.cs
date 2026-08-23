using System;
using System.Collections.Generic;

namespace ADSUS_BE.BLL.MedicalRecord.DTOs;

public sealed record SymptomItemResponse(
    Guid SymptomId,
    string Name,
    bool IsOther);

public sealed record SymptomCategoryResponse(
    Guid CategoryId,
    string Name,
    bool IsOther,
    IReadOnlyList<SymptomItemResponse> Symptoms);
