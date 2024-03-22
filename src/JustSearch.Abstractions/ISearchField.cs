namespace JustSearch.Abstractions;

public interface ISearchField
{
    public string Name { get; init; }
    
    public bool IsNumber { get; init; }
    
    public bool IsFacet { get; init; }
    
    public bool IsFilterable { get; init; }
    
    public bool IsSortable { get; init; }
    
    public bool IsSearchable { get; init; }
    
    public bool IsRetrievable { get; init; }
}
