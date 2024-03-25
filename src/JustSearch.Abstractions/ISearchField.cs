namespace JustSearch.Abstractions;

public enum SearchFieldType
{
    Unknown,
    String,
    Int32,
    Int64,
    Float,
    Bool,
    Object
}

public interface ISearchField
{
    public string Name { get; init; }
    
    public SearchFieldType Type { get; init; }
    
    public bool IsArray { get; init; }
    
    public string? Locale { get; init; }
    
    public bool IsFacet { get; init; }
    
    public bool IsFilterable { get; init; }
    
    public bool IsSortable { get; init; }
    
    public bool IsSearchable { get; init; }
    
    public bool NoTypoTolerance { get; init; }
    
    public bool IsRetrievable { get; init; }
}
