using System.Threading.Channels;
using JustSearch.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace JustSearch;

internal sealed class SearchIndexJobChannel
{
    private readonly Channel<Job> _channel;
    
    internal SearchIndexJobChannel()
    {
        _channel = Channel.CreateBounded<Job>(int.MaxValue);
    }
    
    public ISearchIndexTriggerTask Queue(IEnumerable<Func<IServiceScope, ISearchIndexDataProvider>>? dataProviders)
    {
        var completionSource = new TaskCompletionSource();
        _channel.Writer.TryWrite(new Job(dataProviders, completionSource));
        return new SearchIndexTriggerTask(completionSource.Task);
    }
    
    public ChannelReader<Job> Reader => _channel.Reader;
    
    public record Job(IEnumerable<Func<IServiceScope, ISearchIndexDataProvider>>? dataProviders, TaskCompletionSource completionSource);
}
