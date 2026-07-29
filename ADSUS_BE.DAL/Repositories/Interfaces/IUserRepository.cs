using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

public interface IUserRepository
{
    /// <summary>
    /// Looks up an account by phone number — the system's only login identifier (BR-02).
    /// Returns null when no such account exists; the business layer decides how to respond.
    /// </summary>
    Task<User?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up an account by primary key. Used once the caller is already identified
    /// through a JWT claim.
    /// </summary>
    Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
