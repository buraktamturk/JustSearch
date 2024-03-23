namespace JustSearch.Abstractions;

public interface ISearchIndexDataProvider
{
    public string Name { get; }
    
    IAsyncEnumerable<ISearchField> GetFields();
    
    IAsyncEnumerable<ISynonym> GetSynonyms();

    IAsyncEnumerable<ISearchable> Get(DateTimeOffset? updatedSince = null);
    
    IAsyncEnumerable<string> GetDeleted(DateTimeOffset since);
}