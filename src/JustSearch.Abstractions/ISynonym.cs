namespace JustSearch.Abstractions;

public interface ISynonym
{
     string Id { get; }
     
     IReadOnlyCollection<string> Synonyms { get; }
}
