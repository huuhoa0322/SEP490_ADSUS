using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>EF Core implementation của IInvoiceRepository.</summary>
public sealed class InvoiceRepository : IInvoiceRepository
{
    private readonly AppDbContext _db;

    public InvoiceRepository(AppDbContext db) => _db = db;

    public Task<Guid?> GetActiveIdByCaseAsync(Guid caseId, CancellationToken ct = default) =>
        _db.Invoices
            .AsNoTracking()
            .Where(i => i.CaseId == caseId && (i.Status == InvoiceStatus.PENDING || i.Status == InvoiceStatus.PAID))
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<(IReadOnlyList<Invoice> Items, int TotalCount)> SearchPagedAsync(
        string? search,
        InvoiceStatus? status,
        string? sortBy,
        bool descending,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Invoices
            .AsNoTracking()
            .Include(i => i.Case)
                .ThenInclude(c => c.PatientProfile)
                    .ThenInclude(p => p.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.Trim().ToLower();
            query = query.Where(i => i.Id.ToString().Contains(lower) || i.Case.PatientProfile.User.FullName.ToLower().Contains(lower));
        }

        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        query = sortBy?.ToLower() switch
        {
            "totalamount" => descending ? query.OrderByDescending(i => i.TotalAmount) : query.OrderBy(i => i.TotalAmount),
            _ => descending ? query.OrderByDescending(i => i.CreatedAt) : query.OrderBy(i => i.CreatedAt)
        };

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public Task<Invoice?> GetDetailAsync(Guid invoiceId, CancellationToken ct = default) =>
        _db.Invoices
            .AsNoTracking()
            .Include(i => i.Case)
                .ThenInclude(c => c.PatientProfile)
                    .ThenInclude(p => p.User)
            .Include(i => i.InvoiceItems)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);

    public Task<Invoice?> GetWithItemsForUpdateAsync(Guid invoiceId, CancellationToken ct = default) =>
        _db.Invoices
            .Include(i => i.InvoiceItems)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);

    public Task<Invoice?> GetForUpdateAsync(Guid invoiceId, CancellationToken ct = default) =>
        _db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, ct);

    public Task<string?> GetPatientNameAsync(Guid invoiceId, CancellationToken ct = default) =>
        _db.Invoices
            .AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .Select(i => i.Case.PatientProfile.User == null ? null : i.Case.PatientProfile.User.FullName)
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(Invoice invoice, CancellationToken ct = default) =>
        await _db.Invoices.AddAsync(invoice, ct);

    public async Task AddItemAsync(InvoiceItem item, CancellationToken ct = default) =>
        await _db.InvoiceItems.AddAsync(item, ct);

    public Task<bool> HasPaidByCaseAsync(Guid caseId, CancellationToken ct = default) =>
        _db.Invoices.AnyAsync(i => i.CaseId == caseId && i.Status == InvoiceStatus.PAID, ct);

    public Task<Invoice?> GetPendingWithItemsForUpdateAsync(Guid caseId, CancellationToken ct = default) =>
        _db.Invoices
            .Include(i => i.InvoiceItems)
            .FirstOrDefaultAsync(i => i.CaseId == caseId && i.Status == InvoiceStatus.PENDING, ct);

    public void RemoveItem(InvoiceItem item) => _db.InvoiceItems.Remove(item);
}
