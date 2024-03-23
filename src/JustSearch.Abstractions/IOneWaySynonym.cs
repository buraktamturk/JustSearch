namespace JustSearch.Abstractions;

public interface IOneWaySynonym : ISynonym
{
    public string Root { get; }
}