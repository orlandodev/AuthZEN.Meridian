using Microsoft.Extensions.DependencyInjection;

namespace AuthZen.Pep;

public static class PepServiceCollectionExtensions
{
    // One-line registration so an API becomes a PEP:
    //   builder.Services.AddAuthZenPep("https+http://pdp");
    // The base address uses Aspire service discovery ("pdp" is the AppHost name).
    public static IServiceCollection AddAuthZenPep(this IServiceCollection services, string pdpBaseAddress)
    {
        services.AddHttpClient<IPolicyDecisionClient, AuthZenPolicyDecisionClient>(client =>
        {
            client.BaseAddress = new Uri(pdpBaseAddress);
        });
        return services;
    }
}
