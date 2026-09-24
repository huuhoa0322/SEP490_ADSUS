using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Repositories.Implementations;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Moq;

namespace ADSUS_BE.UnitTests;

/// <summary>
/// MedicineService trước đây đọc/lưu thẳng qua AppDbContext, nên test của nó mock
/// IMedicineRepository nhưng nạp dữ liệu thẳng vào DB InMemory. Phần đó nay đi qua repository
/// (P11 review 24/09/2026) — helper này nối các method MỚI của mock sang MedicineRepository thật
/// trên CÙNG DB InMemory, để dữ liệu test đã nạp vẫn được đọc/lưu như trước. Setup riêng của từng
/// test cho các method cũ giữ nguyên.
/// </summary>
internal static class MedicineRepositoryTestBridge
{
    public static Mock<IMedicineRepository> BackedBy(this Mock<IMedicineRepository> mock, AppDbContext db)
    {
        var real = new MedicineRepository(db);

        mock.Setup(r => r.GetWithBatchesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken ct) => real.GetWithBatchesAsync(id, ct));
        mock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => real.SaveChangesAsync(ct));

        return mock;
    }
}
