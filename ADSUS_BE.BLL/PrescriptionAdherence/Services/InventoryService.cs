using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly IInventoryRepository _inventory;
        private readonly IMedicineRepository _medicines;
        private readonly ISupplierRepository _suppliers;
        private readonly IMedicinePackagingRepository _packagings;
        private readonly IPrescriptionRepository _prescriptions;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<InventoryService> _logger;

        public InventoryService(
            IInventoryRepository inventory,
            IMedicineRepository medicines,
            ISupplierRepository suppliers,
            IMedicinePackagingRepository packagings,
            IPrescriptionRepository prescriptions,
            IUnitOfWork unitOfWork,
            ILogger<InventoryService> logger)
        {
            _inventory = inventory;
            _medicines = medicines;
            _suppliers = suppliers;
            _packagings = packagings;
            _prescriptions = prescriptions;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task ImportMedicineAsync(ImportInventoryRequest request)
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                await ProcessSingleImportAsync(request);
                await transaction.CommitAsync();
            }
            catch (BusinessException)
            {
                await transaction.RollbackAsync();
                throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi nghiêm trọng khi nhập kho cho thuốc {MedicineId}", request.MedicineId);
                throw new Exception("Đã xảy ra lỗi hệ thống trong quá trình nhập kho. Vui lòng thử lại sau.");
            }
        }

        public async Task ImportMedicineBulkAsync(System.Collections.Generic.List<ImportInventoryRequest> requests)
        {
            if (requests == null || requests.Count == 0) return;

            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                for (int i = 0; i < requests.Count; i++)
                {
                    try
                    {
                        await ProcessSingleImportAsync(requests[i]);
                    }
                    catch (BusinessException ex)
                    {
                        throw new BusinessException($"Lỗi ở Hàng số {i + 1}: {ex.Message}");
                    }
                }
                await transaction.CommitAsync();
            }
            catch (BusinessException)
            {
                await transaction.RollbackAsync();
                throw; // Rethrow business exceptions to show validation error to user
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi nghiêm trọng khi nhập kho hàng loạt");
                throw new Exception("Đã xảy ra lỗi hệ thống trong quá trình nhập kho hàng loạt. Vui lòng thử lại sau.");
            }
        }

        private async Task ProcessSingleImportAsync(ImportInventoryRequest request)
        {
                var medicine = await _medicines.GetByIdAsync(request.MedicineId);

                if (medicine == null || medicine.Status != MedicineStatus.Active)
                {
                    throw new BusinessException("Thuốc không tồn tại hoặc đã ngừng sử dụng.");
                }

                var supplier = await _suppliers.GetByIdAsync(request.SupplierId);

                if (supplier == null || !supplier.IsActive)
                {
                    throw new BusinessException("Nhà cung cấp không tồn tại hoặc đã bị khóa.");
                }

                // So với "hôm nay" theo giờ phòng khám — theo UTC thì từ 00:00 đến 07:00 giờ VN lô
                // hết hạn ngay hôm nay vẫn nhập được.
                if (DateOnly.FromDateTime(request.ExpiryDate) <= ClinicClock.Today())
                {
                    throw new BusinessException("Hạn sử dụng phải lớn hơn ngày hiện tại.");
                }

                // 2. Validate Packaging & Calculate Base Unit
                var packaging = await _packagings.GetByIdAsync(request.MedicinePackagingId);

                if (packaging == null || packaging.MedicineId != request.MedicineId)
                {
                    throw new BusinessException("Đơn vị đóng gói không hợp lệ cho thuốc này.");
                }

                var quantityBase = request.Quantity * packaging.ConversionFactor;
                var unitImportPrice = request.ImportPricePerUnit / packaging.ConversionFactor;

                // 3. Upsert MedicineBatch - Validate Lot Number uniqueness across medicines
                var existingLotBatch = await _inventory.GetBatchByLotNumberForUpdateAsync(request.LotNumber);

                MedicineBatch batch;
                if (existingLotBatch != null)
                {
                    if (existingLotBatch.MedicineId != request.MedicineId)
                    {
                        throw new BusinessException($"Mã lô {request.LotNumber} đã được sử dụng cho một loại thuốc khác trong hệ thống. Vui lòng kiểm tra lại.");
                    }

                    // Trùng Lô và trùng Thuốc: So sánh Expiry Date
                    if (existingLotBatch.ExpiryDate != DateOnly.FromDateTime(request.ExpiryDate))
                    {
                        throw new BusinessException("Số lô này đã tồn tại trong kho nhưng khác Hạn sử dụng. Vui lòng kiểm tra lại số lô.");
                    }

                    // Tính giá nhập trung bình gia quyền (Weighted Average)
                    var totalNewQuantity = existingLotBatch.QuantityBase + quantityBase;
                    if (totalNewQuantity > 0)
                    {
                        existingLotBatch.BaseUnitAvgImportPrice = ((existingLotBatch.QuantityBase * existingLotBatch.BaseUnitAvgImportPrice) + (quantityBase * unitImportPrice)) / totalNewQuantity;
                    }

                    existingLotBatch.QuantityBase += quantityBase;
                    batch = existingLotBatch;
                }
                else
                {
                    // Tạo lô mới
                    batch = new MedicineBatch
                    {
                        MedicineId = request.MedicineId,
                        LotNumber = request.LotNumber,
                        ExpiryDate = DateOnly.FromDateTime(request.ExpiryDate),
                        QuantityBase = quantityBase,
                        BaseUnitAvgImportPrice = unitImportPrice
                    };
                    await _inventory.AddBatchAsync(batch);
                }

                // Chờ lưu Batch nếu là Batch mới để có ID
                await _unitOfWork.SaveChangesAsync();

                // 4. Ghi log InventoryTransaction
                var txn = new InventoryTransaction
                {
                    BatchId = batch.Id,
                    MedicinePackagingId = request.MedicinePackagingId,
                    QuantityInUnit = request.Quantity,
                    QuantityBase = quantityBase,
                    TxnType = InventoryTxnType.Import,
                    TxnDate = DateTime.UtcNow,
                    SupplierId = request.SupplierId,
                    ActualImportPrice = unitImportPrice
                };
                await _inventory.AddTransactionAsync(txn);

                // Lưu lại thay đổi của lệnh Import này
                await _unitOfWork.SaveChangesAsync();
        }

        public async Task<PagedResult<InventoryHistoryResponse>> GetInventoryHistoryAsync(InventoryHistoryFilter filter)
        {
            // Sắp xếp động theo filter — mặc định giảm dần (mới nhất trước)
            bool desc = !string.Equals(filter.SortDir, "asc", StringComparison.OrdinalIgnoreCase);

            var (rows, totalCount) = await _inventory.GetHistoryPageAsync(
                filter.Type, filter.BatchId, filter.Search, filter.SortBy, desc, filter.Page, filter.PageSize);

            var items = rows.Select(r => new InventoryHistoryResponse
            {
                TransactionId = r.TransactionId,
                BatchId = r.BatchId,
                LotNumber = r.LotNumber,
                MedicineName = r.MedicineName,
                SupplierName = r.SupplierName,
                UnitName = r.UnitName,
                BaseUnitName = r.BaseUnitName,
                TxnType = r.TxnType,
                QuantityBase = r.QuantityBase,
                QuantityInUnit = r.QuantityInUnit,
                TxnDate = r.TxnDate,
                UnitImportPrice = r.UnitImportPrice,
                PrescriptionItemId = r.PrescriptionItemId,
                Reason = r.Reason
            }).ToList();

            return new PagedResult<InventoryHistoryResponse>(items, filter.Page, filter.PageSize, totalCount, (int)Math.Ceiling(totalCount / (double)filter.PageSize));
        }

        public async Task<ImportValidationResponse> ValidateImportAsync(ImportInventoryRequest request)
        {
            // Chỉ kiểm tra hợp lệ, không sửa gì — mọi truy vấn đều chỉ đọc
            var medicine = await _medicines.GetByIdAsync(request.MedicineId);

            if (medicine == null || medicine.Status != MedicineStatus.Active)
            {
                return new ImportValidationResponse { IsValid = false, ErrorMessage = "Thuốc không tồn tại hoặc đã ngừng sử dụng." };
            }

            var supplier = await _suppliers.GetByIdAsync(request.SupplierId);

            if (supplier == null || !supplier.IsActive)
            {
                return new ImportValidationResponse { IsValid = false, ErrorMessage = "Nhà cung cấp không tồn tại hoặc đã bị khóa." };
            }

            if (DateOnly.FromDateTime(request.ExpiryDate) <= ClinicClock.Today())
            {
                return new ImportValidationResponse { IsValid = false, ErrorMessage = "Hạn sử dụng phải lớn hơn ngày hiện tại." };
            }

            var packaging = await _packagings.GetByIdAsync(request.MedicinePackagingId);

            if (packaging == null || packaging.MedicineId != request.MedicineId)
            {
                return new ImportValidationResponse { IsValid = false, ErrorMessage = "Đơn vị đóng gói không hợp lệ cho thuốc này." };
            }

            var existingLotBatch = await _inventory.GetBatchByLotNumberAsync(request.LotNumber);

            if (existingLotBatch != null)
            {
                if (existingLotBatch.MedicineId != request.MedicineId)
                {
                    return new ImportValidationResponse { IsValid = false, ErrorMessage = $"Mã lô {request.LotNumber} đã được sử dụng cho một loại thuốc khác trong hệ thống." };
                }

                if (existingLotBatch.ExpiryDate != DateOnly.FromDateTime(request.ExpiryDate))
                {
                    return new ImportValidationResponse { IsValid = false, ErrorMessage = "Số lô này đã tồn tại trong kho nhưng khác Hạn sử dụng." };
                }
            }

            return new ImportValidationResponse { IsValid = true };
        }

        public async Task<PagedResult<MedicineBatchResponse>> GetMedicineBatchesAsync(MedicineBatchFilter filter)
        {
            // Sort động — mặc định tăng dần (hạn gần nhất lên trước)
            bool desc = string.Equals(filter.SortDir, "desc", StringComparison.OrdinalIgnoreCase);

            var (rows, totalCount) = await _inventory.GetBatchesPageAsync(
                filter.MedicineId, filter.Search, filter.SortBy, desc, filter.Page, filter.PageSize);

            var items = rows.Select(r => new MedicineBatchResponse
            {
                BatchId = r.BatchId,
                MedicineId = r.MedicineId,
                LotNumber = r.LotNumber,
                ExpiryDate = r.ExpiryDate.ToDateTime(TimeOnly.MinValue),
                QuantityBase = r.QuantityBase,
                BaseUnitAvgImportPrice = r.BaseUnitAvgImportPrice,
                UsageUnit = r.UsageUnit
            }).ToList();

            return new PagedResult<MedicineBatchResponse>(
                items, filter.Page, filter.PageSize, totalCount,
                (int)Math.Ceiling(totalCount / (double)filter.PageSize));
        }
        public async Task DispenseAsync(Guid caseId)
        {
            var prescription = await _prescriptions.GetActiveByCaseWithItemsAsync(caseId);

            if (prescription == null || prescription.PrescriptionItems.Count == 0)
            {
                throw new BusinessException("Không tìm thấy đơn thuốc hoặc đơn thuốc trống.");
            }

            // FEFO: nạp các lô còn hạn, còn hàng của MỌI thuốc trong đơn bằng một truy vấn, sắp theo
            // hạn dùng tăng dần — thay vì truy vấn lại cho từng dòng thuốc (N+1, P11 review 24/09/2026).
            // Có tracking vì số lượng lô bị trừ ngay trên các entity này. "Hôm nay" theo giờ phòng
            // khám — theo UTC thì từ 00:00 đến 07:00 giờ VN lô hết hạn hôm qua vẫn bị xuất.
            var today = ClinicClock.Today();
            var medicineIds = prescription.PrescriptionItems.Select(pi => pi.MedicineId).Distinct().ToList();
            var batchesByMedicine = (await _inventory.ListAvailableBatchesForUpdateAsync(medicineIds, today))
                .ToLookup(b => b.MedicineId);

            foreach (var pItem in prescription.PrescriptionItems)
            {
                decimal volumePerBaseUnit = pItem.Medicine.VolumePerBaseUnit ?? 1m;
                var quantityNeededBS = (int)Math.Ceiling(pItem.QuantityBase / (double)volumePerBaseUnit);
                if (quantityNeededBS <= 0) continue;

                var baseUnitPack = pItem.Medicine.MedicinePackagings.FirstOrDefault(mp => mp.IsBaseUnit);
                if (baseUnitPack == null)
                {
                    throw new BusinessException($"Thuốc '{pItem.Medicine.Name}' chưa được cấu hình Base Unit.");
                }

                foreach (var batch in batchesByMedicine[pItem.MedicineId])
                {
                    if (quantityNeededBS <= 0) break;
                    // Cùng một thuốc có thể xuất hiện ở nhiều dòng — lô đã bị dòng trước lấy hết thì bỏ qua
                    if (batch.QuantityBase <= 0) continue;

                    int cutQtyBS = Math.Min(batch.QuantityBase, quantityNeededBS);

                    batch.QuantityBase -= cutQtyBS;
                    quantityNeededBS -= cutQtyBS;

                    var txn = new InventoryTransaction
                    {
                        Id = Guid.NewGuid(),
                        BatchId = batch.Id,
                        MedicinePackagingId = baseUnitPack.Id,
                        QuantityInUnit = cutQtyBS,
                        QuantityBase = cutQtyBS,
                        TxnDate = DateTime.UtcNow,
                        PrescriptionItemId = pItem.PrescriptionItemId,
                        ActualImportPrice = batch.BaseUnitAvgImportPrice, // ĐÓNG BĂNG GIÁ VỐN
                        TxnType = InventoryTxnType.Dispense
                    };

                    await _inventory.AddTransactionAsync(txn);
                }

                if (quantityNeededBS > 0)
                {
                    throw new BusinessException($"Thuốc '{pItem.Medicine.Name}' không đủ tồn kho hợp lệ. Thiếu {quantityNeededBS} {baseUnitPack?.MedicineUnit?.Name ?? "đơn vị BS"}.");
                }
            }

            await _unitOfWork.SaveChangesAsync();
        }
        public async Task<AdjustInventoryResponse> AdjustAsync(AdjustInventoryRequest request)
        {
            var batch = await _inventory.GetBatchWithPackagingsForUpdateAsync(request.BatchId);

            if (batch == null)
            {
                throw new BusinessException("Lô thuốc không tồn tại.");
            }

            if (request.NewQuantityBase < 0)
            {
                throw new BusinessException("Số lượng thực tế không được âm.");
            }

            int previousQty = batch.QuantityBase;
            int delta = request.NewQuantityBase - previousQty;

            if (delta == 0)
            {
                throw new BusinessException("Số lượng thực tế không thay đổi so với hệ thống.");
            }

            var baseUnitPack = batch.Medicine.MedicinePackagings.FirstOrDefault(mp => mp.IsBaseUnit);
            if (baseUnitPack == null)
            {
                throw new BusinessException($"Thuốc '{batch.Medicine.Name}' chưa được cấu hình Base Unit.");
            }

            batch.QuantityBase = request.NewQuantityBase;

            var txn = new InventoryTransaction
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                MedicinePackagingId = baseUnitPack.Id,
                QuantityInUnit = Math.Abs(delta),
                QuantityBase = delta, // Dương = tăng, Âm = giảm
                TxnDate = DateTime.UtcNow,
                TxnType = InventoryTxnType.Adjustment,
                Reason = request.Reason
            };

            await _inventory.AddTransactionAsync(txn);
            await _unitOfWork.SaveChangesAsync();

            return new AdjustInventoryResponse
            {
                TransactionId = txn.Id,
                PreviousQuantity = previousQty,
                NewQuantity = request.NewQuantityBase,
                Delta = delta
            };
        }

        public async Task<InventoryAlertSummary> GetAlertSummaryAsync()
        {
            var summary = new InventoryAlertSummary();
            // "Hôm nay" theo giờ phòng khám cho cả tồn kho hợp lệ lẫn số ngày còn hạn
            var today = ClinicClock.Today();

            // Báo cáo chỉ đọc — repository không tracking danh mục thuốc và lô
            var allMedicinesQuery = await _inventory.ListActiveMedicineStocksAsync(today);

            summary.TotalMedicinesCount = allMedicinesQuery.Count;
            summary.OutOfStockCount = allMedicinesQuery.Count(x => x.TotalStock == 0);

            // "Còn hàng" là những thuốc tồn kho > ngưỡng cảnh báo
            summary.InStockCount = allMedicinesQuery.Count(x => x.TotalStock > x.Medicine.LowStockThreshold);

            // 1. LOW STOCK (những thuốc tồn <= ngưỡng, ngoại trừ trường hợp không có ngưỡng và hết hàng)
            var lowStockQuery = allMedicinesQuery
                .Where(x => x.Medicine.LowStockThreshold > 0 && x.TotalStock <= x.Medicine.LowStockThreshold)
                .ToList();

            foreach (var stock in lowStockQuery)
            {
                var med = stock.Medicine;
                var baseUnitPack = med.MedicinePackagings.FirstOrDefault(mp => mp.IsBaseUnit);

                summary.LowStockAlerts.Add(new LowStockAlertResponse
                {
                    MedicineId = med.MedicineId,
                    MedicineName = med.Name,
                    CurrentStock = stock.TotalStock,
                    Threshold = med.LowStockThreshold,
                    BaseUnitName = baseUnitPack?.MedicineUnit?.Name ?? "Đơn vị cơ sở",
                    Severity = stock.TotalStock <= (int)Math.Ceiling(med.LowStockThreshold * 0.2m) ? "CRITICAL" : "WARNING"
                });
            }

            summary.LowStockCount = summary.LowStockAlerts.Count;

            // 2. EXPIRY
            var batches = await _inventory.ListBatchesInStockAsync();

            foreach (var batch in batches)
            {
                var daysUntilExpiry = batch.ExpiryDate.DayNumber - today.DayNumber;

                if (daysUntilExpiry <= 60)
                {
                    var baseUnitPack = batch.Medicine.MedicinePackagings.FirstOrDefault(mp => mp.IsBaseUnit);
                    string severity;
                    if (daysUntilExpiry <= 0) severity = "EXPIRED";
                    else if (daysUntilExpiry <= 30) severity = "CRITICAL";
                    else severity = "WARNING";

                    summary.ExpiryAlerts.Add(new ExpiryAlertResponse
                    {
                        BatchId = batch.Id,
                        MedicineId = batch.MedicineId,
                        MedicineName = batch.Medicine.Name,
                        LotNumber = batch.LotNumber,
                        ExpiryDate = batch.ExpiryDate.ToDateTime(TimeOnly.MinValue),
                        DaysUntilExpiry = daysUntilExpiry,
                        QuantityBase = batch.QuantityBase,
                        BaseUnitName = baseUnitPack?.MedicineUnit?.Name ?? "Đơn vị cơ sở",
                        Severity = severity
                    });
                }
            }

            summary.ExpiryAlerts = summary.ExpiryAlerts.OrderBy(a => a.ExpiryDate).ToList();
            summary.ExpiredCount = summary.ExpiryAlerts.Count(a => a.Severity == "EXPIRED");
            summary.ExpiringSoonCount = summary.ExpiryAlerts.Count(a => a.Severity != "EXPIRED");

            return summary;
        }
    }
}
