using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>EF Core implementation của ISupplierRepository.</summary>
public sealed class SupplierRepository : ISupplierRepository
{
    private readonly AppDbContext _db;

    public SupplierRepository(AppDbContext db) => _db = db;

    public async Task<(IReadOnlyList<Supplier> Items, int TotalCount)> SearchPagedAsync(
        string? search,
        int pageIndex,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Suppliers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.ToLower().Trim();
            query = query.Where(s => s.Name.ToLower().Contains(keyword) ||
                                     s.PhoneNumber.Contains(keyword) ||
                                     s.TaxCode.Contains(keyword));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public Task<Supplier?> GetByIdAsync(Guid supplierId, CancellationToken ct = default) =>
        _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.SupplierId == supplierId, ct);

    public Task<Supplier?> GetForUpdateAsync(Guid supplierId, CancellationToken ct = default) =>
        _db.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == supplierId, ct);

    public Task<Supplier?> FindDuplicateAsync(
        string name,
        string phoneNumber,
        string email,
        string? taxCode,
        Guid? excludeSupplierId,
        CancellationToken ct = default)
    {
        var lowerName = name.ToLower();
        var lowerEmail = email.ToLower();

        var query = _db.Suppliers.AsNoTracking();
        if (excludeSupplierId.HasValue)
            query = query.Where(s => s.SupplierId != excludeSupplierId.Value);

        return query
            .Where(s => s.Name.ToLower() == lowerName ||
                        s.PhoneNumber == phoneNumber ||
                        s.Email.ToLower() == lowerEmail ||
                        (taxCode != null && s.TaxCode == taxCode))
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(Supplier supplier, CancellationToken ct = default) =>
        await _db.Suppliers.AddAsync(supplier, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
