namespace ADSUS_BE.BLL.Common.Events;

/// <summary>
/// Publisher interface for domain events.
/// Components publish events through this interface.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes a domain event to all registered handlers.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IDomainEvent;
}
