using F1Telemetry.Contracts;

namespace F1Telemetry.Ingestion.Internal;

internal sealed class InProcessEventBus : IEventBus
{
    private readonly object _lock = new();
    private readonly Dictionary<Type, List<Delegate>> _handlers = [];

    public void Publish<TEvent>(TEvent payload) where TEvent : class
    {
        Delegate[] snapshot;
        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var handlers))
            {
                return;
            }

            snapshot = [.. handlers];
        }

        foreach (var handler in snapshot)
        {
            ((Action<TEvent>)handler)(payload);
        }
    }

    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
    {
        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var handlers))
            {
                _handlers[typeof(TEvent)] = handlers = [];
            }

            handlers.Add(handler);
        }
    }
}
