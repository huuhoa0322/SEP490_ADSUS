using System.Threading.Tasks;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Interfaces
{
    public interface IInventoryService
    {
        Task ImportMedicineAsync(ImportInventoryRequest request);
        Task ImportMedicineBulkAsync(System.Collections.Generic.List<ImportInventoryRequest> requests);
        Task<ADSUS_BE.BLL.Common.PagedResult<InventoryHistoryResponse>> GetInventoryHistoryAsync(InventoryHistoryFilter filter);
        Task<System.Collections.Generic.List<MedicineBatchResponse>> GetMedicineBatchesAsync(System.Guid medicineId);
        Task<ImportValidationResponse> ValidateImportAsync(ImportInventoryRequest request);
    }
}
