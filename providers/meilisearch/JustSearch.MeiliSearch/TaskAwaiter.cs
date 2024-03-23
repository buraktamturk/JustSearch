using Meilisearch;

namespace JustSearch.MeiliSearch;

internal class TaskAwaiter
{
    private readonly Queue<int> _taskIds
        = new();

    private readonly MeilisearchClient _client;
    
    public TaskAwaiter(MeilisearchClient client)
    {
        _client = client;
    }

    public void AddTaskResponse(TaskInfo taskInfo)
    {
        if (taskInfo.Status is TaskInfoStatus.Succeeded)
            return;
        
        if (taskInfo.Status is not TaskInfoStatus.Enqueued)
            throw new InvalidOperationException($"Task failed: {taskInfo.Error}.");
        
        _taskIds.Enqueue(taskInfo.TaskUid);
    }

    public async Task WaitForTasks(CancellationToken token = default)
    {
        while (_taskIds.TryDequeue(out var taskId) && !token.IsCancellationRequested)
        {
            var taskInfo = await _client.WaitForTaskAsync(taskId, cancellationToken: token);
            if (taskInfo.Status is not TaskInfoStatus.Succeeded)
                throw new InvalidOperationException($"Task failed: {taskInfo.Error}.");
        }
    }
    
    public static async Task WaitForSingleTask(MeilisearchClient client, int taskId, CancellationToken token = default)
    {
        var taskInfo = await client.WaitForTaskAsync(taskId, cancellationToken: token);
        if (taskInfo.Status is not TaskInfoStatus.Succeeded)
            throw new InvalidOperationException($"Task failed: {taskInfo.Error}.");
    }
}