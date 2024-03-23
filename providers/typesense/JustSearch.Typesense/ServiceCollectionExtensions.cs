using JustSearch.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Typesense.Setup;

namespace JustSearch.Typesense;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTypeSenseProvider(this IServiceCollection serviceCollection, IConfiguration configuration)
    {
        var apiKey = configuration["ApiKey"];
        var host = new Uri(configuration["Url"]);

        return serviceCollection
            .AddTypeSenseProvider(config =>
            {
                config.ApiKey = apiKey;
                config.Nodes = new List<Node>
                {
                    new Node(host.Host, host.Port.ToString(), host.Scheme)
                };
            });
    }
    
    public static IServiceCollection AddTypeSenseProvider(this IServiceCollection serviceCollection, Action<Config> config)
    {
        return serviceCollection
            .AddTypesenseClient(config)
            .AddTypeSenseProvider();
    }
    
    public static IServiceCollection AddTypeSenseProvider(this IServiceCollection serviceCollection)
    {
        return serviceCollection
            .AddSingleton<ISearchIndexProvider, TypesenseProvider>();
    }
}