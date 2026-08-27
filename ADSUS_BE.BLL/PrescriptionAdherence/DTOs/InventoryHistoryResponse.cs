using System;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs
{
    public class InventoryHistoryFilter
    {
        public string? Search { get; set; }
        public InventoryTxnType? Type { get; set; }
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
        public string UnitName { get; set; } = null!;
        public InventoryTxnType TxnType { get; set; }
        public int QuantityBase { get; set; }
        public int QuantityInUnit { get; set; }
        public DateTime TxnDate { get; set; }
        public decimal UnitImportPrice { get; set; }
        public Guid? PrescriptionItemId { get; set; }
    }
}
