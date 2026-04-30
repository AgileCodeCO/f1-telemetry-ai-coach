using F1Telemetry.Agents.Extensions;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;

namespace F1Telemetry.UnitTests.Agents;

public sealed class LlmClientFactoryTests
{
    private static IConfiguration BuildConfig(string provider, string baseUrl = "http://localhost:11434") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LLM:Provider"] = provider,
                ["LLM:Model"] = "test-model",
                ["LLM:BaseUrl"] = baseUrl,
                ["LLM:ApiKey"] = "test-key",
            })
            .Build();

    [Fact]
    public void AddAgents_Ollama_ResolvesOllamaApiClient()
    {
        ServiceProvider sp = new ServiceCollection()
            .AddAgents(BuildConfig("ollama"))
            .BuildServiceProvider();

        IChatClient client = sp.GetRequiredService<IChatClient>();
        client.Should().BeOfType<OllamaApiClient>();
    }

    [Fact]
    public void AddAgents_UnknownProvider_ThrowsOnResolve()
    {
        ServiceProvider sp = new ServiceCollection()
            .AddAgents(BuildConfig("unknown-provider"))
            .BuildServiceProvider();

        Action act = () => sp.GetRequiredService<IChatClient>();
        act.Should().Throw<InvalidOperationException>().WithMessage("*unknown-provider*");
    }
}
