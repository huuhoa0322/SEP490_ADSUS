using System;
using System.Threading.Tasks;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs.Invoice;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;

public interface IInvoiceService
{
    /// <summary>
    /// Sinh hóa đơn (Greedy Allocation) cho toàn bộ thuốc của một Ca khám.
    /// Lấy danh sách thuốc, sắp xếp đơn vị bán lẻ từ lớn đến nhỏ, áp dụng Volume Discount.
    /// Trả về ID của hóa đơn vừa tạo.
    /// </summary>
    Task<Guid> GenerateInvoiceForCaseAsync(Guid caseId);

    Task<PagedResult<InvoiceResponse>> GetInvoicesAsync(InvoiceFilter filter);
    Task<InvoiceDetailResponse> GetInvoiceDetailAsync(Guid id);
    Task PayAndDispenseAsync(Guid invoiceId, PaymentMethod method);
    
    /// <summary>
    /// Hủy hóa đơn. Nếu đã PAID → reverse dispense (hoàn kho tự động).
    /// </summary>
    Task CancelInvoiceAsync(Guid invoiceId, CancelInvoiceRequest request);
}
