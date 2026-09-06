namespace ADSUS_BE.BLL.Common.Events;

/// <summary>
/// Handler interface for domain events.
/// Each event type has its own handler registered in DI.
/// </summary>
public interface IEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    /// <summary>
    /// Handles the domain event.
    /// </summary>
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}
