namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Ranh giới lưu dữ liệu cho Service — thay cho việc Service tự cầm AppDbContext để mở
/// transaction hay gọi SaveChanges (P11 review 24/09/2026: Service không chạm DbContext).
/// Mọi repository dùng chung một AppDbContext theo scope request, nên một lệnh
/// <see cref="SaveChangesAsync"/> lưu mọi thay đổi đang được track của mọi repository.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Mở transaction. Provider không hỗ trợ transaction (DB InMemory của test) nhận về một
    /// transaction rỗng — Commit/Rollback không làm gì, giống cách code cũ bỏ qua transaction.
    /// </summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>Lưu mọi thay đổi đang được track.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Transaction do <see cref="IUnitOfWork.BeginTransactionAsync"/> mở. Dispose mà chưa Commit = Rollback.</summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable, IDisposable
{
    Task CommitAsync(CancellationToken ct = default);

    Task RollbackAsync(CancellationToken ct = default);
}
