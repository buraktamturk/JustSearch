using JustSearch.Abstractions;

namespace JustSearch;

internal sealed record IndexPrefix(string Prefix) : IIndexPrefix;