using JustSearch.Abstractions;

namespace JustSearch;

public record Synonym(string Id, IReadOnlyCollection<string> Synonyms) : ISynonym;