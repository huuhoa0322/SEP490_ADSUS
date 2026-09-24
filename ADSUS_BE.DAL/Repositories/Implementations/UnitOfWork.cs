using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>EF Core implementation của IUnitOfWork trên AppDbContext theo scope request.</summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    public UnitOfWork(AppDbContext db) => _db = db;

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        if (!_db.Database.IsRelational())
            return NoOpTransaction.Instance;

        return new EfTransaction(await _db.Database.BeginTransactionAsync(ct));
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    private sealed class EfTransaction : IUnitOfWorkTransaction
    {
        private readonly IDbContextTransaction _inner;

        public EfTransaction(IDbContextTransaction inner) => _inner = inner;

        public Task CommitAsync(CancellationToken ct = default) => _inner.CommitAsync(ct);

        public Task RollbackAsync(CancellationToken ct = default) => _inner.RollbackAsync(ct);

        public ValueTask DisposeAsync() => _inner.DisposeAsync();

        public void Dispose() => _inner.Dispose();
    }

    private sealed class NoOpTransaction : IUnitOfWorkTransaction
    {
        public static readonly NoOpTransaction Instance = new();

        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Dispose() { }
    }
}
