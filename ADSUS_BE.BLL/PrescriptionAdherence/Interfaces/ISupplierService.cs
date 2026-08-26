using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.Common;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;

public interface ISupplierService
{
    Task<PagedResult<SupplierResponse>> GetSuppliersAsync(int pageIndex, int pageSize, string? search, CancellationToken ct = default);
    Task<SupplierResponse> GetSupplierByIdAsync(Guid supplierId, CancellationToken ct = default);
    Task<SupplierResponse> CreateSupplierAsync(CreateSupplierRequest request, CancellationToken ct = default);
    Task<SupplierResponse> UpdateSupplierAsync(Guid supplierId, UpdateSupplierRequest request, CancellationToken ct = default);
    Task UpdateSupplierStatusAsync(Guid supplierId, bool isActive, CancellationToken ct = default);
}
