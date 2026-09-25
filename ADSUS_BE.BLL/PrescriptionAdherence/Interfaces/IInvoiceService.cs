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

    /// <summary>
    /// Tự sinh hoá đơn khi ca kết thúc: chỉ sinh nếu ca CHƯA có hoá đơn còn hiệu lực và CÓ dịch vụ
    /// hoặc đơn thuốc đang hiệu lực. Trả về Id hoá đơn vừa sinh, null nếu không cần sinh. Module khác
    /// (kê đơn, kết thúc ca) gọi hàm này thay vì tự đọc bảng hoá đơn/dịch vụ để quyết định.
    /// </summary>
    Task<Guid?> GenerateInvoiceIfBillableAsync(Guid caseId);

    /// <summary>Ca đã có hoá đơn đã thanh toán (PAID) chưa — khi đó không được thêm/gỡ dịch vụ.</summary>
    Task<bool> HasPaidInvoiceAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>
    /// Dịch vụ vừa gắn vào ca: nếu ca đang có hoá đơn PENDING thì thêm dòng dịch vụ và cộng tổng
    /// tiền. CHƯA lưu — bên gọi lưu cùng lượt với bản ghi dịch vụ.
    /// </summary>
    Task StageServiceAddedAsync(Guid caseId, Guid caseClinicServiceId, string description, decimal price, CancellationToken ct = default);

    /// <summary>
    /// Dịch vụ vừa gỡ khỏi ca: nếu hoá đơn PENDING có dòng của dịch vụ đó thì bỏ dòng, trừ tổng
    /// tiền, và tự huỷ hoá đơn khi không còn dòng nào. CHƯA lưu — bên gọi lưu cùng lượt.
    /// </summary>
    Task StageServiceRemovedAsync(Guid caseId, Guid caseClinicServiceId, CancellationToken ct = default);

    Task<PagedResult<InvoiceResponse>> GetInvoicesAsync(InvoiceFilter filter);
    Task<InvoiceDetailResponse> GetInvoiceDetailAsync(Guid id);
    Task PayInvoiceAsync(Guid invoiceId, PaymentMethod method);
    
    /// <summary>
    /// Hủy hóa đơn. Nếu đã PAID → reverse dispense (hoàn kho tự động).
    /// </summary>
    Task CancelInvoiceAsync(Guid invoiceId, CancelInvoiceRequest request);

    /// <summary>
    /// Xóa một dòng thuốc khỏi hóa đơn PENDING: hoàn kho (reverse dispense) cho dòng thuốc đó,
    /// tính lại tổng tiền hóa đơn. Chỉ áp dụng cho hóa đơn PENDING và item loại Medicine.
    /// </summary>
    Task RemoveMedicineItemAsync(Guid invoiceId, Guid invoiceItemId, CancellationToken ct = default);
}
