using System.Threading.Tasks;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Interfaces
{
    public interface IInventoryService
    {
        Task ImportMedicineAsync(ImportInventoryRequest request);
        Task ImportMedicineBulkAsync(System.Collections.Generic.List<ImportInventoryRequest> requests);
        Task<ADSUS_BE.BLL.Common.PagedResult<InventoryHistoryResponse>> GetInventoryHistoryAsync(InventoryHistoryFilter filter);
        Task<ADSUS_BE.BLL.Common.PagedResult<MedicineBatchResponse>> GetMedicineBatchesAsync(MedicineBatchFilter filter);

        Task<ImportValidationResponse> ValidateImportAsync(ImportInventoryRequest request);
        
        /// <summary>
        /// Xuất kho dựa trên đơn thuốc (FEFO algorithm).
        /// Cắt QuantityBase từ các lô cũ nhất, sinh InventoryTransaction đóng băng giá vốn.
        /// </summary>
        Task DispenseAsync(System.Guid caseId);

        /// <summary>
        /// Điều chỉnh kho (kiểm kê): cập nhật QuantityBase thực tế cho một lô,
        /// ghi InventoryTransaction type Adjustment kèm lý do.
        /// </summary>
        Task<AdjustInventoryResponse> AdjustAsync(AdjustInventoryRequest request);

        Task<InventoryAlertSummary> GetAlertSummaryAsync();
    }
}
