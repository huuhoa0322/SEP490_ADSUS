using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

public interface ICaseRepository
{
    /// <summary>Bản đầy đủ cho màn hình chi tiết (#23) và cho PDF (#27).</summary>
    Task<Case?> GetDetailAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>Bản nhẹ, KHÔNG theo dõi (AsNoTracking) — chỉ để kiểm tra tồn tại và trạng thái.</summary>
    Task<Case?> GetByIdAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>Bản CÓ theo dõi — dùng khi cần sửa entity rồi gọi SaveChangesAsync (vd. ConfirmAsync).</summary>
    Task<Case?> GetForUpdateAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>
    /// <paramref name="statuses"/> null hoặc rỗng = không lọc trạng thái (mọi Case của patient
    /// này). Có giá trị = chỉ lấy Case nằm trong tập đó — dùng tập thay vì 1 status đơn vì
    /// ListMineAsync cần lọc CẢ Confirmed lẫn End cùng lúc (patient vẫn phải thấy ca đã kê đơn).
    /// </summary>
    Task<(IReadOnlyList<Case> Items, int TotalCount)> SearchByPatientAsync(
        Guid patientProfileId,
        IReadOnlyCollection<CaseStatus>? statuses,
        string sortOrder,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Tạo ca bệnh (dùng cho booking từ Mobile - không có images).
    /// </summary>
    Task<Case> CreateAsync(Case newCase, CancellationToken ct = default);

    /// <summary>
    /// Ghi ca bệnh và toàn bộ ảnh trong MỘT lần SaveChanges — không bao giờ tồn tại ca
    /// không có ảnh nào (UC-07 BR-02).
    /// </summary>
    Task<Case> CreateWithImagesAsync(
        Case newCase,
        IReadOnlyList<UltrasoundImage> images,
        CancellationToken ct = default);

    /// <summary>Lưu thay đổi trên một Case đã tải qua GetForUpdateAsync (bản CÓ theo dõi) —
    /// dùng cho các cập nhật đơn giản như SaveConclusionAsync/ConfirmAsync. KHÔNG dùng với
    /// entity tải qua GetByIdAsync/GetDetailAsync — hai hàm đó dùng AsNoTracking(), sửa xong
    /// gọi SaveChangesAsync sẽ không ghi được gì (EF không theo dõi để biết mà lưu).</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
