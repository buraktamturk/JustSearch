namespace JustSearch;

public record SearchField(string Name, bool IsNumber = false, bool IsFacet = false, bool IsFilterable = false, bool IsSortable = false, bool IsSearchable = false, bool IsRetrievable = true);