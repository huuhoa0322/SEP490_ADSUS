using System;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs
{
    public class MedicineBatchResponse
    {
        public Guid BatchId { get; set; }
        public Guid MedicineId { get; set; }
        public string LotNumber { get; set; } = null!;
        public DateTime ExpiryDate { get; set; }
        public int QuantityBase { get; set; }
        public decimal BaseUnitAvgImportPrice { get; set; }
        public string? UsageUnit { get; set; }   // Đơn vị cơ bản của thuốc
    }
}
