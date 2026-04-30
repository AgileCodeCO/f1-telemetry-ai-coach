using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using F1Telemetry.Agents.Options;
using Microsoft.Extensions.AI;

namespace F1Telemetry.Agents.Clients;

internal sealed class AnthropicChatClient(IHttpClientFactory httpFactory, LlmOptions opts) : IChatClient
{
    public ChatClientMetadata Metadata => new("anthropic", new Uri(opts.BaseUrl), opts.Model);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        IList<ChatMessage> messages = chatMessages as IList<ChatMessage> ?? [.. chatMessages];

        using HttpClient http = httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        http.DefaultRequestHeaders.Add("x-api-key", opts.ApiKey);
        http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        string systemPrompt = messages
            .Where(m => m.Role == ChatRole.System)
            .Select(m => m.Text ?? string.Empty)
            .FirstOrDefault(string.Empty);

        var userMessages = messages
            .Where(m => m.Role == ChatRole.User)
            .Select(m => new { role = "user", content = m.Text ?? string.Empty })
            .ToArray();

        var requestBody = new
        {
            model = opts.Model,
            max_tokens = 1024,
            system = systemPrompt,
            messages = userMessages
        };

        using HttpResponseMessage response = await http.PostAsJsonAsync(
            new Uri(opts.BaseUrl.TrimEnd('/') + "/v1/messages"),
            requestBody,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        using JsonDocument doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        string text = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
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
