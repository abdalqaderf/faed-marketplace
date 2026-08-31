using Faed.Application.Merchants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Faed.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers application use-case services. External seams (persistence, storage,
    /// identity) are provided by Infrastructure (docs/06-ARCHITECTURE.md §3, §8).
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MerchantVerificationOptions>()
            .Bind(configuration.GetSection(MerchantVerificationOptions.SectionName));

        services.AddScoped<IMerchantVerificationService, MerchantVerificationService>();

        return services;
    }
}
