using JustSearch.Abstractions;

namespace JustSearch;

public record Searchable : ISearchable
{
    public string Id { get; init; }
}
