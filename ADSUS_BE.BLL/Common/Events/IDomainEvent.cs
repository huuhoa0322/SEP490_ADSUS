namespace ADSUS_BE.BLL.Common.Events;

/// <summary>
/// Base interface for all domain events.
/// Domain events represent something significant that happened in the domain.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// When the event occurred.
    /// </summary>
    DateTime OccurredAt { get; }

    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    Guid EventId { get; }
}
