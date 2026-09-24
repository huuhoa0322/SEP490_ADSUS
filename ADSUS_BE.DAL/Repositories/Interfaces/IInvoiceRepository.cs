using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Repository cho hoá đơn của ca khám (Invoice + InvoiceItem). Mỗi ca có tối đa một hoá đơn
/// còn hiệu lực (PENDING hoặc PAID); hoá đơn huỷ giữ lại để đối soát.
/// </summary>
public interface IInvoiceRepository
{
    /// <summary>Id hoá đơn còn hiệu lực (PENDING/PAID) của ca, null nếu chưa có.</summary>
    Task<Guid?> GetActiveIdByCaseAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>
    /// Danh sách hoá đơn phân trang kèm Case → PatientProfile → User (để lấy tên bệnh nhân).
    /// <paramref name="search"/> khớp một phần mã hoá đơn hoặc tên bệnh nhân. Chỉ đọc.
    /// </summary>
    Task<(IReadOnlyList<Invoice> Items, int TotalCount)> SearchPagedAsync(
        string? search,
        InvoiceStatus? status,
        string? sortBy,
        bool descending,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>Chi tiết hoá đơn kèm các dòng và tên bệnh nhân. Chỉ đọc.</summary>
    Task<Invoice?> GetDetailAsync(Guid invoiceId, CancellationToken ct = default);

    /// <summary>Hoá đơn kèm các dòng — CÓ tracking.</summary>
    Task<Invoice?> GetWithItemsForUpdateAsync(Guid invoiceId, CancellationToken ct = default);

    /// <summary>Hoá đơn (không kèm dòng) — CÓ tracking.</summary>
    Task<Invoice?> GetForUpdateAsync(Guid invoiceId, CancellationToken ct = default);

    /// <summary>Tên bệnh nhân của hoá đơn (qua ca khám), null nếu không xác định được.</summary>
    Task<string?> GetPatientNameAsync(Guid invoiceId, CancellationToken ct = default);

    /// <summary>Thêm hoá đơn vào context, CHƯA lưu.</summary>
    Task AddAsync(Invoice invoice, CancellationToken ct = default);

    /// <summary>Thêm một dòng hoá đơn vào context, CHƯA lưu.</summary>
    Task AddItemAsync(InvoiceItem item, CancellationToken ct = default);
}
