using System.Net;
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
                    .Handle<HttpRequestException>(IsTransient)
                    .Handle<TaskCanceledException>()
                    .Handle<TimeoutException>()
            })
            .Build();

    // Only retry transient failures. 4xx are configuration errors — retrying won't help.
    private static bool IsTransient(HttpRequestException ex) =>
        ex.StatusCode is null
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

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
