using System;
using System.ComponentModel.DataAnnotations;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs
{
    public class ImportInventoryRequest
    {
        [Required(ErrorMessage = "Vui lòng chọn thuốc.")]
        public Guid MedicineId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn nhà cung cấp.")]
        public Guid SupplierId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn đơn vị nhập.")]
        public Guid MedicinePackagingId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số lô.")]
        [StringLength(100, ErrorMessage = "Số lô không được vượt quá 100 ký tự.")]
        [RegularExpression(@"^[a-zA-Z0-9]+([-_][a-zA-Z0-9]+)*$", ErrorMessage = "Số lô chỉ được chứa chữ, số, dấu gạch ngang/dưới (không nằm ở đầu/cuối, không đứng liền nhau).")]
        public string LotNumber { get; set; } = null!;

        [Required(ErrorMessage = "Vui lòng nhập hạn sử dụng.")]
        public DateTime ExpiryDate { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số lượng.")]
        [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0.")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giá nhập.")]
        [Range(0, double.MaxValue, ErrorMessage = "Giá nhập không được là số âm.")]
        public decimal ImportPricePerUnit { get; set; }
    }
}
