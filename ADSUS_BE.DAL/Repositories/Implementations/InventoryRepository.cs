using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>EF Core implementation của IInventoryRepository.</summary>
public sealed class InventoryRepository : IInventoryRepository
{
    private readonly AppDbContext _db;

    public InventoryRepository(AppDbContext db) => _db = db;

    // ─── Lô thuốc ────────────────────────────────────────────────────────────────

    public Task<MedicineBatch?> GetBatchByLotNumberAsync(string lotNumber, CancellationToken ct = default) =>
        _db.MedicineBatches.AsNoTracking().FirstOrDefaultAsync(b => b.LotNumber == lotNumber, ct);

    public Task<MedicineBatch?> GetBatchByLotNumberForUpdateAsync(string lotNumber, CancellationToken ct = default) =>
        _db.MedicineBatches.FirstOrDefaultAsync(b => b.LotNumber == lotNumber, ct);

    public Task<MedicineBatch?> GetBatchWithPackagingsForUpdateAsync(Guid batchId, CancellationToken ct = default) =>
        _db.MedicineBatches
            .Include(b => b.Medicine)
                .ThenInclude(m => m.MedicinePackagings)
            .FirstOrDefaultAsync(b => b.Id == batchId, ct);

    public async Task<IReadOnlyList<MedicineBatch>> ListAvailableBatchesForUpdateAsync(
        IReadOnlyCollection<Guid> medicineIds,
        DateOnly today,
        CancellationToken ct = default)
    {
        return await _db.MedicineBatches
            .Where(b => medicineIds.Contains(b.MedicineId) && b.QuantityBase > 0 && b.ExpiryDate >= today)
            .OrderBy(b => b.ExpiryDate)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetAvailableBaseQuantitiesAsync(
        IReadOnlyCollection<Guid> medicineIds,
        DateOnly today,
        CancellationToken ct = default)
    {
        return await _db.MedicineBatches
            .AsNoTracking()
            .Where(b => medicineIds.Contains(b.MedicineId) && b.QuantityBase > 0 && b.ExpiryDate >= today)
            .GroupBy(b => b.MedicineId)
            .Select(g => new { MedicineId = g.Key, Total = g.Sum(b => b.QuantityBase) })
            .ToDictionaryAsync(x => x.MedicineId, x => x.Total, ct);
    }

    public async Task<(IReadOnlyList<MedicineBatchRow> Items, int TotalCount)> GetBatchesPageAsync(
        Guid medicineId,
        string? search,
        string? sortBy,
        bool descending,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.MedicineBatches
            .AsNoTracking()
            .Where(b => b.MedicineId == medicineId);

        // Search theo Số lô
        if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.Trim().ToLower();
            query = query.Where(b => b.LotNumber.ToLower().Contains(lower));
        }

        query = sortBy?.ToLower() switch
        {
            "quantitybase" => descending
                ? query.OrderByDescending(b => b.QuantityBase)
                : query.OrderBy(b => b.QuantityBase),
            "avgprice" => descending
                ? query.OrderByDescending(b => b.BaseUnitAvgImportPrice)
                : query.OrderBy(b => b.BaseUnitAvgImportPrice),
            _ => descending
                ? query.OrderByDescending(b => b.ExpiryDate)
                : query.OrderBy(b => b.ExpiryDate), // mặc định: hạn gần nhất lên trước
        };

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new MedicineBatchRow(
                b.Id,
                b.MedicineId,
                b.LotNumber,
                b.ExpiryDate,
                b.QuantityBase,
                b.BaseUnitAvgImportPrice,
                // Tên đơn vị cơ bản từ MedicinePackaging có IsBaseUnit = true
                _db.MedicinePackagings
                    .Where(mp => mp.MedicineId == b.MedicineId && mp.IsBaseUnit)
                    .Select(mp => mp.MedicineUnit.Name)
                    .FirstOrDefault()))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<MedicineStockRow>> ListActiveMedicineStocksAsync(DateOnly today, CancellationToken ct = default)
    {
        var rows = await _db.Medicines
            .AsNoTracking()
            .Include(m => m.MedicinePackagings)
                .ThenInclude(mp => mp.MedicineUnit)
            .Where(m => m.Status == MedicineStatus.Active)
            .Select(m => new
            {
                Medicine = m,
                TotalStock = m.MedicineBatches
                    .Where(b => b.QuantityBase > 0 && b.ExpiryDate >= today)
                    .Sum(b => (int?)b.QuantityBase) ?? 0
            })
            .ToListAsync(ct);

        return rows.Select(r => new MedicineStockRow(r.Medicine, r.TotalStock)).ToList();
    }

    public async Task<IReadOnlyList<MedicineBatch>> ListBatchesInStockAsync(CancellationToken ct = default) =>
        await _db.MedicineBatches
            .AsNoTracking()
            .Include(b => b.Medicine)
                .ThenInclude(m => m.MedicinePackagings)
                    .ThenInclude(mp => mp.MedicineUnit)
            .Where(b => b.QuantityBase > 0)
            .ToListAsync(ct);

    public async Task AddBatchAsync(MedicineBatch batch, CancellationToken ct = default) =>
        await _db.MedicineBatches.AddAsync(batch, ct);

    // ─── Giao dịch kho ───────────────────────────────────────────────────────────

    public async Task<(IReadOnlyList<InventoryHistoryRow> Items, int TotalCount)> GetHistoryPageAsync(
        InventoryTxnType? type,
        Guid? batchId,
        string? search,
        string? sortBy,
        bool descending,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.InventoryTransactions
            .AsNoTracking()
            .Join(_db.MedicinePackagings,
                txn => txn.MedicinePackagingId,
                mp => mp.Id,
                (txn, mp) => new { txn, mp });

        if (type.HasValue)
        {
            query = query.Where(q => q.txn.TxnType == type.Value);
        }

        if (batchId.HasValue)
        {
            query = query.Where(q => q.txn.BatchId == batchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowerSearch = search.Trim().ToLower();
            query = query.Where(q =>
                q.txn.Batch.Medicine.Name.ToLower().Contains(lowerSearch) ||
                q.txn.Batch.LotNumber.ToLower().Contains(lowerSearch) ||
                (q.txn.Supplier != null && q.txn.Supplier.Name.ToLower().Contains(lowerSearch))
            );
        }

        query = sortBy?.ToLower() switch
        {
            "quantitybase" => descending
                ? query.OrderByDescending(q => q.txn.QuantityBase)
                : query.OrderBy(q => q.txn.QuantityBase),
            _ => descending
                ? query.OrderByDescending(q => q.txn.TxnDate)
                : query.OrderBy(q => q.txn.TxnDate),
        };

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(q => new InventoryHistoryRow(
                q.txn.Id,
                q.txn.BatchId,
                q.txn.Batch.LotNumber,
                q.txn.Batch.Medicine.Name,
                q.txn.Supplier != null ? q.txn.Supplier.Name : null,
                q.mp.MedicineUnit.Name,
                // Tên đơn vị cơ bản từ MedicinePackaging có IsBaseUnit = true
                _db.MedicinePackagings
                    .Where(bp => bp.MedicineId == q.txn.Batch.MedicineId && bp.IsBaseUnit)
                    .Select(bp => bp.MedicineUnit.Name)
                    .FirstOrDefault(),
                q.txn.TxnType,
                q.txn.QuantityBase,
                q.txn.QuantityInUnit,
                q.txn.TxnDate,
                (q.txn.ActualImportPrice ?? q.txn.Batch.BaseUnitAvgImportPrice) * q.mp.ConversionFactor,
                q.txn.PrescriptionItemId,
                q.txn.Reason))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<InventoryTransaction>> ListDispenseTransactionsForUpdateAsync(
        IReadOnlyCollection<Guid> prescriptionItemIds,
        CancellationToken ct = default)
    {
        return await _db.InventoryTransactions
            .Include(t => t.Batch)
            .Where(t => t.TxnType == InventoryTxnType.Dispense
                        && t.PrescriptionItemId.HasValue
                        && prescriptionItemIds.Contains(t.PrescriptionItemId.Value))
            .ToListAsync(ct);
    }

    public async Task AddTransactionAsync(InventoryTransaction transaction, CancellationToken ct = default) =>
        await _db.InventoryTransactions.AddAsync(transaction, ct);

    public void RemoveTransaction(InventoryTransaction transaction) => _db.InventoryTransactions.Remove(transaction);
}
