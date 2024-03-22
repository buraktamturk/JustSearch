using JustSearch.Abstractions;

namespace JustSearch;

internal sealed class SearchIndexDataProviderProxy : ISearchIndexDataProvider
{
    private readonly ISearchIndexDataProvider _searchIndexDataProviderImplementation;
    private readonly IAsyncEnumerable<ISearchable>? _items;
    private readonly IAsyncEnumerable<string>? _itemsToDelete;
    
    internal SearchIndexDataProviderProxy(
        ISearchIndexDataProvider searchIndexDataProviderImplementation,
        IAsyncEnumerable<ISearchable>? items = null,
        IAsyncEnumerable<string>? itemsToDelete = null
    )
    {
        _searchIndexDataProviderImplementation = searchIndexDataProviderImplementation;
        _items = items;
        _itemsToDelete = itemsToDelete;
    }
    
    public string Name => _searchIndexDataProviderImplementation.Name;

    public IAsyncEnumerable<ISearchField> GetFields()
    {
        return _searchIndexDataProviderImplementation.GetFields();
    }

    public IAsyncEnumerable<ISearchable> Get(DateTimeOffset? updatedSince = null)
    {
        return _items ?? AsyncEnumerable.Empty<Searchable>();
    }

    public IAsyncEnumerable<string> GetDeleted(DateTimeOffset since)
    {
        return _itemsToDelete ?? AsyncEnumerable.Empty<string>();
    }
}