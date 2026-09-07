using System;
using System.ComponentModel.DataAnnotations;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs.Invoice;

public class CancelInvoiceRequest
{
    [Required(ErrorMessage = "Vui lòng nhập lý do hủy hóa đơn.")]
    [StringLength(500, ErrorMessage = "Lý do không được vượt quá 500 ký tự.")]
    public string Reason { get; set; } = null!;
}
