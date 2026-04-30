using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace F1Telemetry.IntegrationTests.Harness;

internal sealed class StubLlmClient : IChatClient
{
    public string ResponseToReturn { get; set; } =
        """{"finding":"Consistent brake point improvements available in S1","estimated_gain_ms":350}""";

    public string? LastSystemPrompt { get; private set; }
    public string? LastUserPrompt { get; private set; }

#pragma warning disable CA1822 // interface implementation cannot be static
    public ChatClientMetadata Metadata => new();
#pragma warning restore CA1822

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        IList<ChatMessage> messages = chatMessages as IList<ChatMessage> ?? [.. chatMessages];
        LastSystemPrompt = messages.FirstOrDefault(m => m.Role == ChatRole.System)?.Text;
        LastUserPrompt = messages.FirstOrDefault(m => m.Role == ChatRole.User)?.Text;
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, ResponseToReturn)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatResponse response = await GetResponseAsync(chatMessages, options, cancellationToken);
        yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
