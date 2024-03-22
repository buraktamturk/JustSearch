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
            .AddSingleton<JustSearchOptions>()
            .AddSingleton<SearchIndexJobChannel>()
            .AddSingleton<ISearchIndexTrigger, SearchIndexTrigger>()
            .AddHostedService<JustSearchBackgroundService>();
    }
}
