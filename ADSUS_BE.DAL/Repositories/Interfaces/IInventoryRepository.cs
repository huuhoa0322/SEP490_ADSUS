using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Repository cho kho thuốc: lô thuốc (MedicineBatch, tồn kho theo đơn vị cơ sở) và sổ giao
/// dịch kho (InventoryTransaction: nhập, xuất theo đơn, điều chỉnh).
/// </summary>
public interface IInventoryRepository
{
    // ─── Lô thuốc ────────────────────────────────────────────────────────────────

    /// <summary>Lô theo số lô (số lô là duy nhất toàn hệ thống). Chỉ đọc.</summary>
    Task<MedicineBatch?> GetBatchByLotNumberAsync(string lotNumber, CancellationToken ct = default);

    /// <summary>Như <see cref="GetBatchByLotNumberAsync"/> nhưng CÓ tracking — nhập thêm vào lô đã có.</summary>
    Task<MedicineBatch?> GetBatchByLotNumberForUpdateAsync(string lotNumber, CancellationToken ct = default);

    /// <summary>Một lô kèm thuốc và các quy cách đóng gói của thuốc — CÓ tracking (điều chỉnh tồn kho).</summary>
    Task<MedicineBatch?> GetBatchWithPackagingsForUpdateAsync(Guid batchId, CancellationToken ct = default);

    /// <summary>
    /// Các lô còn hàng, còn hạn (hạn &gt;= <paramref name="today"/>) của các thuốc cho trước, hạn gần
    /// nhất trước (FEFO) — CÓ tracking vì bên gọi trừ số lượng ngay trên các lô này.
    /// </summary>
    Task<IReadOnlyList<MedicineBatch>> ListAvailableBatchesForUpdateAsync(
        IReadOnlyCollection<Guid> medicineIds,
        DateOnly today,
        CancellationToken ct = default);

    /// <summary>Tổng tồn kho còn hạn (đơn vị cơ sở) của từng thuốc — thuốc không còn hàng không có trong kết quả.</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetAvailableBaseQuantitiesAsync(
        IReadOnlyCollection<Guid> medicineIds,
        DateOnly today,
        CancellationToken ct = default);

    /// <summary>Danh sách lô của một thuốc (phân trang, tìm theo số lô, sắp xếp động). Chỉ đọc.</summary>
    Task<(IReadOnlyList<MedicineBatchRow> Items, int TotalCount)> GetBatchesPageAsync(
        Guid medicineId,
        string? search,
        string? sortBy,
        bool descending,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>Thuốc đang hoạt động kèm quy cách đóng gói + tổng tồn còn hạn — cho cảnh báo tồn thấp. Chỉ đọc.</summary>
    Task<IReadOnlyList<MedicineStockRow>> ListActiveMedicineStocksAsync(DateOnly today, CancellationToken ct = default);

    /// <summary>Mọi lô còn hàng kèm thuốc + quy cách đóng gói — cho cảnh báo hạn dùng. Chỉ đọc.</summary>
    Task<IReadOnlyList<MedicineBatch>> ListBatchesInStockAsync(CancellationToken ct = default);

    /// <summary>Thêm lô vào context, CHƯA lưu.</summary>
    Task AddBatchAsync(MedicineBatch batch, CancellationToken ct = default);

    // ─── Giao dịch kho ───────────────────────────────────────────────────────────

    /// <summary>Lịch sử giao dịch kho (phân trang, lọc, tìm kiếm, sắp xếp động). Chỉ đọc.</summary>
    Task<(IReadOnlyList<InventoryHistoryRow> Items, int TotalCount)> GetHistoryPageAsync(
        InventoryTxnType? type,
        Guid? batchId,
        string? search,
        string? sortBy,
        bool descending,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>Giao dịch xuất kho theo các dòng đơn thuốc, kèm lô — CÓ tracking (hoàn kho khi huỷ hoá đơn).</summary>
    Task<IReadOnlyList<InventoryTransaction>> ListDispenseTransactionsForUpdateAsync(
        IReadOnlyCollection<Guid> prescriptionItemIds,
        CancellationToken ct = default);

    /// <summary>Thêm giao dịch vào context, CHƯA lưu.</summary>
    Task AddTransactionAsync(InventoryTransaction transaction, CancellationToken ct = default);

    /// <summary>Đánh dấu xoá giao dịch, CHƯA lưu.</summary>
    void RemoveTransaction(InventoryTransaction transaction);
}

/// <summary>Một lô trong danh sách lô của thuốc — UsageUnit là tên đơn vị cơ sở của thuốc.</summary>
public sealed record MedicineBatchRow(
    Guid BatchId,
    Guid MedicineId,
    string LotNumber,
    DateOnly ExpiryDate,
    int QuantityBase,
    decimal BaseUnitAvgImportPrice,
    string? UsageUnit);

/// <summary>Thuốc kèm tổng tồn còn hạn (đơn vị cơ sở). Medicine có sẵn MedicinePackagings + MedicineUnit.</summary>
public sealed record MedicineStockRow(Medicine Medicine, int TotalStock);

/// <summary>Một dòng lịch sử kho — UnitName là đơn vị của giao dịch, BaseUnitName là đơn vị cơ sở của thuốc.</summary>
public sealed record InventoryHistoryRow(
    Guid TransactionId,
    Guid BatchId,
    string LotNumber,
    string MedicineName,
    string? SupplierName,
    string UnitName,
    string? BaseUnitName,
    InventoryTxnType TxnType,
    int QuantityBase,
    int QuantityInUnit,
    DateTime TxnDate,
    decimal UnitImportPrice,
    Guid? PrescriptionItemId,
    string? Reason);
