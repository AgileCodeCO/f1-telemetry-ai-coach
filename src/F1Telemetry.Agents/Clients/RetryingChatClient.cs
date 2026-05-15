using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Polly;
using Polly.Retry;

namespace F1Telemetry.Agents.Clients;

internal sealed class RetryingChatClient(IChatClient inner) : IChatClient
{
    private static readonly ResiliencePipeline Pipeline =
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(1),
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .Handle<TimeoutException>()
            })
            .Build();

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => Pipeline.ExecuteAsync(
                ct => new ValueTask<ChatResponse>(inner.GetResponseAsync(chatMessages, options, ct)),
                cancellationToken)
            .AsTask();

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => inner.GetStreamingResponseAsync(chatMessages, options, cancellationToken);

    public object? GetService(Type serviceType, object? serviceKey = null)
        => inner.GetService(serviceType, serviceKey);

    public void Dispose() => inner.Dispose();
}
