namespace JustSearch.Abstractions;

public interface ISearchIndexProvider
{
    public string Name { get; }
    
    public Task<int> CreateOrUpdateIndexAsync(ISearchIndexDataProvider dataProvider, CancellationToken token = default);
}