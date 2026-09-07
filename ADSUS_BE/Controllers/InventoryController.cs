using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace ADSUS_BE.Controllers
{
    [ApiController]
    [Route("api/v1/inventory")]
    [Authorize(Roles = "ADMIN,PHARMACIST")] // Temporary admin permission as requested
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryService _inventoryService;

        public InventoryController(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }

        [HttpPost("import")]
        public async Task<IActionResult> ImportMedicine([FromBody] ImportInventoryRequest request)
        {
            await _inventoryService.ImportMedicineAsync(request);
            return Ok(new { message = "Nhập kho thành công." });
        }

        [HttpPost("validate-import")]
        public async Task<IActionResult> ValidateImport([FromBody] ImportInventoryRequest request)
        {
            var result = await _inventoryService.ValidateImportAsync(request);
            return Ok(result);
        }

        [HttpPost("import/bulk")]
        public async Task<IActionResult> ImportMedicineBulk([FromBody] System.Collections.Generic.List<ImportInventoryRequest> requests)
        {
            await _inventoryService.ImportMedicineBulkAsync(requests);
            return Ok(new { message = $"Đã nhập thành công {requests.Count} danh mục thuốc." });
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetInventoryHistory([FromQuery] InventoryHistoryFilter filter)
        {
            var result = await _inventoryService.GetInventoryHistoryAsync(filter);
            return Ok(result);
        }

        [HttpGet("batches")]
        public async Task<IActionResult> GetMedicineBatches([FromQuery] MedicineBatchFilter filter)
        {
            var result = await _inventoryService.GetMedicineBatchesAsync(filter);
            return Ok(result);
        }

        [HttpPut("adjust")]
        public async Task<IActionResult> AdjustInventory([FromBody] AdjustInventoryRequest request)
        {
            var result = await _inventoryService.AdjustAsync(request);
            return Ok(result);
        }

        [HttpGet("alerts")]
        public async Task<IActionResult> GetAlerts()
        {
            var result = await _inventoryService.GetAlertSummaryAsync();
            return Ok(result);
        }
    }
}
