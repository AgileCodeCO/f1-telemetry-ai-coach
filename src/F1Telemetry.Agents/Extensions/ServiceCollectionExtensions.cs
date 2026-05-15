using F1Telemetry.Agents.Agents;
using F1Telemetry.Agents.Clients;
using F1Telemetry.Agents.Internal;
using F1Telemetry.Agents.Options;
using F1Telemetry.Agents.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OpenAI;
using OpenAI.Chat;

namespace F1Telemetry.Agents.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgents(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LlmOptions>(o => configuration.GetSection("LLM").Bind(o));
        services.AddHttpClient();
        services.AddSingleton<IChatClient>(sp =>
        {
            LlmOptions opts = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
            IHttpClientFactory httpFactory = sp.GetRequiredService<IHttpClientFactory>();
            IChatClient inner = opts.Provider.ToLowerInvariant() switch
            {
                "ollama" => new OllamaApiClient(new Uri(opts.BaseUrl), opts.Model),
                "lmstudio" => new OpenAIClient(
                        new System.ClientModel.ApiKeyCredential("lm-studio"),
                        new OpenAIClientOptions { Endpoint = new Uri(opts.BaseUrl) })
                    .GetChatClient(opts.Model).AsIChatClient(),
                "openai" => new OpenAIClient(
                        new System.ClientModel.ApiKeyCredential(opts.ApiKey))
                    .GetChatClient(opts.Model).AsIChatClient(),
                "anthropic" => new AnthropicChatClient(httpFactory, opts),
                _ => throw new InvalidOperationException($"Unknown LLM provider: {opts.Provider}")
            };
            return new RetryingChatClient(inner);
        });
        services.AddSingleton<ILapAgent, DeltaAgent>();
        services.AddSingleton<ILapAgent, BrakingAgent>();
        services.AddSingleton<ILapAgent, CornerAgent>();
        services.AddSingleton<ILapAgent, TyreAgent>();
        services.AddSingleton<ILapAgent, RacingLineAgent>();
        services.AddHostedService<AgentOrchestrator>();
        return services;
    }
}
