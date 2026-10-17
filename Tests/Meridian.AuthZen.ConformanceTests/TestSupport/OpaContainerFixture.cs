using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace Meridian.AuthZen.ConformanceTests.TestSupport;

// A real Open Policy Agent server loaded with Meridian.Pdp.Opa's Rego
// policies, shared across one test class. Same image and tag as the "opa"
// resource in AppHost.cs — keep the two pins in step.
public sealed class OpaContainerFixture : IAsyncLifetime
{
    private const int OpaPort = 8181;

    private readonly IContainer _opa = new ContainerBuilder()
        .WithImage("openpolicyagent/opa:1.20.2")
        .WithResourceMapping(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "policies")), "/policies/")
        .WithCommand("run", "--server", "--addr", $":{OpaPort}", "--log-level", "error", "/policies")
        .WithPortBinding(OpaPort, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilHttpRequestIsSucceeded(request => request.ForPort(OpaPort).ForPath("/health")))
        .Build();

    public Uri BaseAddress { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _opa.StartAsync();
        BaseAddress = new Uri($"http://{_opa.Hostname}:{_opa.GetMappedPublicPort(OpaPort)}/");
    }

    public async Task DisposeAsync()
    {
        await _opa.DisposeAsync();
    }
}
