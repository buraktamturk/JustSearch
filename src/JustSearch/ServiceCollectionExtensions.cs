using JustSearch.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace JustSearch;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJustSearch(this IServiceCollection serviceCollection, JustSearchOptions? options = null)
    {
        options ??= new JustSearchOptions();

        return serviceCollection
            .AddSingleton<IIndexPrefix>(new IndexPrefix(options.Prefix))
            .AddSingleton(options)
            .AddSingleton<SearchIndexJobChannel>()
            .AddSingleton<ISearchIndexTrigger, SearchIndexTrigger>()
            .AddHostedService<JustSearchBackgroundService>();
    }
    
    public static IServiceCollection AddSearchIndexDataProvider<T>(this IServiceCollection serviceCollection)
        where T : class, ISearchIndexDataProvider
    {
        return serviceCollection.AddScoped<ISearchIndexDataProvider, T>();
    }
}
