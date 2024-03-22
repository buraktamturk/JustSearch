namespace JustSearch.Abstractions;

public interface ISearchIndexTrigger
{
    ISearchIndexTriggerTask SyncAll();
    
    ISearchIndexTriggerTask Sync<T>()
        where T : ISearchIndexDataProvider;
    
    ISearchIndexTriggerTask Upsert<T>(ISearchable item)
        where T : ISearchIndexDataProvider;
    
    ISearchIndexTriggerTask Upsert<T>(IEnumerable<ISearchable> items)
        where T : ISearchIndexDataProvider;
    
    ISearchIndexTriggerTask Upsert<T>(IAsyncEnumerable<ISearchable> items)
        where T : ISearchIndexDataProvider;
    
    ISearchIndexTriggerTask Delete<T>(string id)
        where T : ISearchIndexDataProvider;
    
    ISearchIndexTriggerTask Delete<T>(IEnumerable<string> ids)
        where T : ISearchIndexDataProvider;
    
    ISearchIndexTriggerTask Delete<T>(IAsyncEnumerable<string> ids)
        where T : ISearchIndexDataProvider;
}

public interface ISearchIndexTriggerTask
{
    Task WaitAsync(CancellationToken cancellationToken);
}
