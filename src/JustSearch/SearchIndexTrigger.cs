using JustSearch.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace JustSearch;

internal sealed class SearchIndexTrigger : ISearchIndexTrigger
{
    private readonly SearchIndexJobChannel _dataProviderChannel;
    
    public SearchIndexTrigger(SearchIndexJobChannel dataProviderChannel)
    {
        _dataProviderChannel = dataProviderChannel;
    }
    
    public ISearchIndexTriggerTask SyncAll()
    {
        return _dataProviderChannel.Queue(null);
    }
    
    private static ISearchIndexDataProvider CreateDataProvider<T>(IServiceScope serviceScope) where T : ISearchIndexDataProvider
    {
        return serviceScope.ServiceProvider
            .GetRequiredService<IEnumerable<ISearchIndexDataProvider>>()
            .First(a => a is T);
    }

    public ISearchIndexTriggerTask Sync<T>() where T : ISearchIndexDataProvider
    {
        return _dataProviderChannel.Queue([static e => CreateDataProvider<T>(e)]);
    }

    public ISearchIndexTriggerTask Upsert<T>(IAsyncEnumerable<ISearchable> items) where T : ISearchIndexDataProvider
    {
        return _dataProviderChannel.Queue([e => new SearchIndexDataProviderProxy(
            CreateDataProvider<T>(e),
            items: items
        )]);
    }
    
    public ISearchIndexTriggerTask Upsert<T>(IEnumerable<ISearchable> items) where T : ISearchIndexDataProvider
    {
        return Upsert<T>(items.ToAsyncEnumerable());
    }
    
    public ISearchIndexTriggerTask Upsert<T>(ISearchable item) where T : ISearchIndexDataProvider
    {
        return Upsert<T>([item]);
    }
    
    public ISearchIndexTriggerTask Delete<T>(IEnumerable<string> ids) where T : ISearchIndexDataProvider
    {
        return Delete<T>(ids.ToAsyncEnumerable());
    }
    
    public ISearchIndexTriggerTask Delete<T>(IAsyncEnumerable<string> ids) where T : ISearchIndexDataProvider
    {
        return _dataProviderChannel.Queue([e => new SearchIndexDataProviderProxy(
            CreateDataProvider<T>(e),
            itemsToDelete: ids
        )]);
    }

    public ISearchIndexTriggerTask Delete<T>(string id) where T : ISearchIndexDataProvider
    {
        return Delete<T>([id]);
    }
}