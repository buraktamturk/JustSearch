using JustSearch.Abstractions;

namespace JustSearch;

public record SearchField(string Name, SearchFieldType Type, bool IsArray = false, string? Locale = null, bool IsFacet = false, bool IsFilterable = false, bool IsSortable = false, bool IsSearchable = false, bool IsRetrievable = true)
    : ISearchField;
    