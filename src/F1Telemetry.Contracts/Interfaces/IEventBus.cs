namespace F1Telemetry.Contracts;

public interface IEventBus
{
    void Publish<TEvent>(TEvent payload) where TEvent : class;
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;
}
