using System;
using System.ComponentModel.DataAnnotations;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

public class AdjustInventoryRequest
{
    [Required(ErrorMessage = "Vui lòng chọn lô thuốc.")]
    public Guid BatchId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số lượng thực tế.")]
    [Range(0, int.MaxValue, ErrorMessage = "Số lượng thực tế không được âm.")]
    public int NewQuantityBase { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do điều chỉnh.")]
    [StringLength(500, ErrorMessage = "Lý do không được vượt quá 500 ký tự.")]
    public string Reason { get; set; } = null!;
}
