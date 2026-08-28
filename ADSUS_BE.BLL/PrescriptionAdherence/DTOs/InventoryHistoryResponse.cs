using System;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs
{
    public class InventoryHistoryFilter
    {
        public string? Search { get; set; }
        public InventoryTxnType? Type { get; set; }
        public Guid? BatchId { get; set; }
        public string? SortBy { get; set; }   // txnDate | quantityBase
        public string? SortDir { get; set; }  // asc | desc (default desc)
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class MedicineBatchFilter
    {
        public Guid MedicineId { get; set; }
        public string? Search { get; set; }   // Tìm theo LotNumber
        public string? SortBy { get; set; }   // expiryDate | quantityBase | avgPrice
        public string? SortDir { get; set; }  // asc | desc (default asc by expiryDate)
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class InventoryHistoryResponse
    {
        public Guid TransactionId { get; set; }
        public Guid BatchId { get; set; }
        public string LotNumber { get; set; } = null!;
        public string MedicineName { get; set; } = null!;
        public string? SupplierName { get; set; }
        public string UnitName { get; set; } = null!;         // Đơn vị đóng gói
        public string? BaseUnitName { get; set; }             // Đơn vị cơ bản (UsageUnit của thuốc)
        public InventoryTxnType TxnType { get; set; }
        public int QuantityBase { get; set; }
        public int QuantityInUnit { get; set; }
        public DateTime TxnDate { get; set; }
        public decimal UnitImportPrice { get; set; }
        public Guid? PrescriptionItemId { get; set; }
    }
}
