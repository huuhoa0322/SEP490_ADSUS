using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.Common.Events;

/// <summary>
/// In-memory event publisher implementation.
/// Uses IServiceProvider to resolve handlers registered in DI.
/// </summary>
public class EventPublisher : IEventPublisher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventPublisher> _logger;

    public EventPublisher(IServiceProvider serviceProvider, ILogger<EventPublisher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IDomainEvent
    {
        _logger.LogInformation(
            "[EventPublisher] Publishing event {EventType} (ID: {EventId})",
            typeof(TEvent).Name,
            @event.EventId);

        using var scope = _serviceProvider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IEventHandler<TEvent>>();

        foreach (var handler in handlers)
        {
            try
            {
                _logger.LogDebug(
                    "[EventPublisher] Invoking handler {HandlerType} for {EventType}",
                    handler.GetType().Name,
                    typeof(TEvent).Name);

                await handler.HandleAsync(@event, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[EventPublisher] Handler {HandlerType} failed for {EventType}",
                    handler.GetType().Name,
                    typeof(TEvent).Name);
                // Continue to next handler - don't fail the whole event dispatch
            }
        }

        _logger.LogInformation(
            "[EventPublisher] Finished publishing event {EventType} to {HandlerCount} handlers",
            typeof(TEvent).Name,
            handlers.Count());
    }
}
