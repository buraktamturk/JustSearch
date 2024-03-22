using JustSearch.Abstractions;

namespace JustSearch;

internal sealed class SearchIndexTriggerTask : ISearchIndexTriggerTask
{
    private readonly Task _task;

    internal SearchIndexTriggerTask(Task task)
    {
        _task = task;
    }

    public Task WaitAsync(CancellationToken cancellationToken)
    {
        return _task.WaitAsync(cancellationToken);
    }
}