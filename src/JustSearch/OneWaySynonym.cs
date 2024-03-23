using JustSearch.Abstractions;

namespace JustSearch;

public record OneWaySynonym(string Id, string Root, IReadOnlyCollection<string> Synonyms) : IOneWaySynonym;