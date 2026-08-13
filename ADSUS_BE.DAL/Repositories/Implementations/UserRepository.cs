using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db) => _db = db;

    public Task<User?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Phone == phone, cancellationToken);

    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

    public Task<User?> GetByIdReadOnlyAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

    public Task<User?> GetForUpdateAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

    public Task<bool> IsEmailUsedByAnotherUserAsync(
        Guid userId,
        string email,
        CancellationToken cancellationToken = default) =>
        _db.Users.AnyAsync(
            u => u.UserId != userId
                 && u.Email != null
                 && u.Email.ToLower() == email.ToLower(),
            cancellationToken);

    public Task<bool> PhoneExistsAsync(string phone, CancellationToken cancellationToken = default) =>
        _db.Users.AnyAsync(u => u.Phone == phone, cancellationToken);

    public Task<bool> IsEmailUsedAsync(string email, CancellationToken cancellationToken = default) =>
        _db.Users.AnyAsync(
            u => u.Email != null && u.Email.ToLower() == email.ToLower(),
            cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await _db.Users.AddAsync(user, cancellationToken);

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> SearchAsync(
        string? keyword,
        UserRole? role,
        UserStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(k) || u.Phone.Contains(k));
        }

        if (role is not null) query = query.Where(u => u.Role == role);
        if (status is not null) query = query.Where(u => u.Status == status);

        // Đếm TRƯỚC khi phân trang, để giao diện biết tổng số bản ghi.
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<User>> ListActiveDoctorsAsync(
        CancellationToken cancellationToken = default) =>
        await _db.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Doctor && u.Status == UserStatus.Active)
            .OrderBy(u => u.FullName)
            .ToListAsync(cancellationToken);
}
