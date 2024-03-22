using Algolia.Search.Clients;
using JustSearch.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JustSearch.Algolia;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAlgoliaProvider(this IServiceCollection serviceCollection, IConfiguration configuration)
    {
        var applicationId = configuration["ApplicationId"];
        var writeKey = configuration["WriteKey"];

        return serviceCollection
            .AddAlgoliaProvider(applicationId, writeKey);
    }
    
    public static IServiceCollection AddAlgoliaProvider(this IServiceCollection serviceCollection, string applicationId, string writeKey)
    {
        return serviceCollection
            .AddSingleton(new SearchClient(applicationId, writeKey))
            .AddAlgoliaProvider();
    }
    
    public static IServiceCollection AddAlgoliaProvider(this IServiceCollection serviceCollection, SearchClient searchClient)
    {
        return serviceCollection
            .AddSingleton(searchClient)
            .AddAlgoliaProvider();
    }
    
    public static IServiceCollection AddAlgoliaProvider(this IServiceCollection serviceCollection)
    {
        return serviceCollection
            .AddSingleton<ISearchIndexProvider, AlgoliaProvider>();
    }
}