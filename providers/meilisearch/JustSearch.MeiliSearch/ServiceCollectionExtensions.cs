using JustSearch.Abstractions;
using Meilisearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JustSearch.MeiliSearch;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMeiliSearchProvider(this IServiceCollection serviceCollection, IConfiguration configuration)
    {
        var url = configuration["Url"];
        var apiKey = configuration["ApiKey"];

        return serviceCollection
            .AddMeiliSearchProvider(url, apiKey);
    }
    
    public static IServiceCollection AddMeiliSearchProvider(this IServiceCollection serviceCollection, string url, string apiKey)
    {
        return serviceCollection
            .AddMeiliSearchProvider(new MeilisearchClient(url, apiKey));
    }
    
    public static IServiceCollection AddMeiliSearchProvider(this IServiceCollection serviceCollection, MeilisearchClient searchClient)
    {
        return serviceCollection
            .AddSingleton(searchClient)
            .AddMeiliSearchProvider();
    }
    
    public static IServiceCollection AddMeiliSearchProvider(this IServiceCollection serviceCollection)
    {
        return serviceCollection
            .AddSingleton<ISearchIndexProvider, MeiliSearchProvider>();
    }
}