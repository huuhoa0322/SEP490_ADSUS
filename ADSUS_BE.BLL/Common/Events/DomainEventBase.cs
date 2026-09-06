namespace ADSUS_BE.BLL.Common.Events;

/// <summary>
/// Base class for domain events with common properties.
/// </summary>
public abstract class DomainEventBase : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
