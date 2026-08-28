using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<InventoryService> _logger;

        public InventoryService(AppDbContext dbContext, ILogger<InventoryService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task ImportMedicineAsync(ImportInventoryRequest request)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
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
            if (requests == null || !requests.Any()) return;

            using var transaction = await _dbContext.Database.BeginTransactionAsync();
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
                var medicine = await _dbContext.Medicines
                    .FirstOrDefaultAsync(m => m.MedicineId == request.MedicineId);
                
                if (medicine == null || medicine.Status != MedicineStatus.Active)
                {
                    throw new BusinessException("Thuốc không tồn tại hoặc đã ngừng sử dụng.");
                }

                var supplier = await _dbContext.Suppliers
                    .FirstOrDefaultAsync(s => s.SupplierId == request.SupplierId);
                
                if (supplier == null || !supplier.IsActive)
                {
                    throw new BusinessException("Nhà cung cấp không tồn tại hoặc đã bị khóa.");
                }

                if (request.ExpiryDate.Date <= DateTime.UtcNow.Date)
                {
                    throw new BusinessException("Hạn sử dụng phải lớn hơn ngày hiện tại.");
                }

                // 2. Validate Packaging & Calculate Base Unit
                var packaging = await _dbContext.MedicinePackagings
                    .FirstOrDefaultAsync(p => p.Id == request.MedicinePackagingId);
                
                if (packaging == null || packaging.MedicineId != request.MedicineId)
                {
                    throw new BusinessException("Đơn vị đóng gói không hợp lệ cho thuốc này.");
                }

                var quantityBase = request.Quantity * packaging.ConversionFactor;
                var unitImportPrice = request.ImportPricePerUnit / packaging.ConversionFactor;

                // 3. Upsert MedicineBatch - Validate Lot Number uniqueness across medicines
                var existingLotBatch = await _dbContext.MedicineBatches
                    .FirstOrDefaultAsync(b => b.LotNumber == request.LotNumber);
                
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
                    _dbContext.MedicineBatches.Add(batch);
                }

                // Chờ lưu Batch nếu là Batch mới để có ID
                await _dbContext.SaveChangesAsync();

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
                _dbContext.InventoryTransactions.Add(txn);

                // Lưu lại thay đổi của lệnh Import này
                await _dbContext.SaveChangesAsync();
        }

        public async Task<PagedResult<InventoryHistoryResponse>> GetInventoryHistoryAsync(InventoryHistoryFilter filter)
        {
            var query = _dbContext.InventoryTransactions
                .Include(t => t.Batch)
                .ThenInclude(b => b.Medicine)
                .Include(t => t.Supplier)
                .Join(_dbContext.MedicinePackagings.Include(mp => mp.MedicineUnit),
                    txn => txn.MedicinePackagingId,
                    mp => mp.Id,
                    (txn, mp) => new { txn, mp })
                .AsQueryable();

            if (filter.Type.HasValue)
            {
                query = query.Where(q => q.txn.TxnType == filter.Type.Value);
            }

            if (filter.BatchId.HasValue)
            {
                query = query.Where(q => q.txn.BatchId == filter.BatchId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var lowerSearch = filter.Search.Trim().ToLower();
                query = query.Where(q => 
                    q.txn.Batch.Medicine.Name.ToLower().Contains(lowerSearch) ||
                    q.txn.Batch.LotNumber.ToLower().Contains(lowerSearch) ||
                    (q.txn.Supplier != null && q.txn.Supplier.Name.ToLower().Contains(lowerSearch))
                );
            }

            // Sắp xếp động theo filter
            bool desc = !string.Equals(filter.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
            query = filter.SortBy?.ToLower() switch
            {
                "quantitybase" => desc
                    ? query.OrderByDescending(q => q.txn.QuantityBase)
                    : query.OrderBy(q => q.txn.QuantityBase),
                _ => desc
                    ? query.OrderByDescending(q => q.txn.TxnDate)
                    : query.OrderBy(q => q.txn.TxnDate),
            };

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(q => new InventoryHistoryResponse
                {
                    TransactionId = q.txn.Id,
                    BatchId = q.txn.BatchId,
                    LotNumber = q.txn.Batch.LotNumber,
                    MedicineName = q.txn.Batch.Medicine.Name,
                    SupplierName = q.txn.Supplier != null ? q.txn.Supplier.Name : null,
                    UnitName = q.mp.MedicineUnit.Name,
                    // Lấy tên đơn vị cơ bản từ MedicinePackaging có IsBaseUnit = true
                    BaseUnitName = _dbContext.MedicinePackagings
                        .Where(bp => bp.MedicineId == q.txn.Batch.MedicineId && bp.IsBaseUnit)
                        .Select(bp => bp.MedicineUnit.Name)
                        .FirstOrDefault(),
                    TxnType = q.txn.TxnType,
                    QuantityBase = q.txn.QuantityBase,
                    QuantityInUnit = q.txn.QuantityInUnit,
                    TxnDate = q.txn.TxnDate,
                    UnitImportPrice = (q.txn.ActualImportPrice ?? q.txn.Batch.BaseUnitAvgImportPrice) * q.mp.ConversionFactor,
                    PrescriptionItemId = q.txn.PrescriptionItemId
                })
                .ToListAsync();

            return new PagedResult<InventoryHistoryResponse>(items, filter.Page, filter.PageSize, totalCount, (int)Math.Ceiling(totalCount / (double)filter.PageSize));
        }

        public async Task<ImportValidationResponse> ValidateImportAsync(ImportInventoryRequest request)
        {
            var medicine = await _dbContext.Medicines
                .FirstOrDefaultAsync(m => m.MedicineId == request.MedicineId);
            
            if (medicine == null || medicine.Status != MedicineStatus.Active)
            {
                return new ImportValidationResponse { IsValid = false, ErrorMessage = "Thuốc không tồn tại hoặc đã ngừng sử dụng." };
            }

            var supplier = await _dbContext.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierId == request.SupplierId);
            
            if (supplier == null || !supplier.IsActive)
            {
                return new ImportValidationResponse { IsValid = false, ErrorMessage = "Nhà cung cấp không tồn tại hoặc đã bị khóa." };
            }

            if (request.ExpiryDate.Date <= DateTime.UtcNow.Date)
            {
                return new ImportValidationResponse { IsValid = false, ErrorMessage = "Hạn sử dụng phải lớn hơn ngày hiện tại." };
            }

            var packaging = await _dbContext.MedicinePackagings
                .FirstOrDefaultAsync(p => p.Id == request.MedicinePackagingId);
            
            if (packaging == null || packaging.MedicineId != request.MedicineId)
            {
                return new ImportValidationResponse { IsValid = false, ErrorMessage = "Đơn vị đóng gói không hợp lệ cho thuốc này." };
            }

            var existingLotBatch = await _dbContext.MedicineBatches
                .FirstOrDefaultAsync(b => b.LotNumber == request.LotNumber);
            
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
            var query = _dbContext.MedicineBatches
                .Where(b => b.MedicineId == filter.MedicineId)
                .AsQueryable();

            // Search theo Số lô
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var lower = filter.Search.Trim().ToLower();
                query = query.Where(b => b.LotNumber.ToLower().Contains(lower));
            }

            // Sort động
            bool desc = string.Equals(filter.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
            query = filter.SortBy?.ToLower() switch
            {
                "quantitybase" => desc
                    ? query.OrderByDescending(b => b.QuantityBase)
                    : query.OrderBy(b => b.QuantityBase),
                "avgprice" => desc
                    ? query.OrderByDescending(b => b.BaseUnitAvgImportPrice)
                    : query.OrderBy(b => b.BaseUnitAvgImportPrice),
                _ => desc
                    ? query.OrderByDescending(b => b.ExpiryDate)
                    : query.OrderBy(b => b.ExpiryDate), // mặc định: hạn gần nhất lên trước
            };

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(b => new MedicineBatchResponse
                {
                    BatchId = b.Id,
                    MedicineId = b.MedicineId,
                    LotNumber = b.LotNumber,
                    ExpiryDate = b.ExpiryDate.ToDateTime(TimeOnly.MinValue),
                    QuantityBase = b.QuantityBase,
                    BaseUnitAvgImportPrice = b.BaseUnitAvgImportPrice,
                    // Lấy tên đơn vị cơ bản từ MedicinePackaging có IsBaseUnit = true
                    UsageUnit = _dbContext.MedicinePackagings
                        .Where(mp => mp.MedicineId == b.MedicineId && mp.IsBaseUnit)
                        .Select(mp => mp.MedicineUnit.Name)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return new PagedResult<MedicineBatchResponse>(
                items, filter.Page, filter.PageSize, totalCount,
                (int)Math.Ceiling(totalCount / (double)filter.PageSize));
        }
    }
}
