using System.Text.Json;
using System.Text.Json.Serialization;
using JustSearch.Abstractions;
using Meilisearch;
using Microsoft.Extensions.Logging;

namespace JustSearch.MeiliSearch;

public sealed class MeiliSearchProvider : ISearchIndexProvider
{
    public string Name => "MeiliSearch";
    
    private readonly MeilisearchClient _client;
    private readonly IIndexPrefix _indexPrefix;
    private readonly ILogger _logger;
    
    private readonly JsonSerializerOptions _jsonOptionsCamelCaseIgnoreWritingNull = new()
    {
        Converters =
        {
            new DictJsonConverter()
        },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    public MeiliSearchProvider(MeilisearchClient client, IIndexPrefix indexPrefix, ILogger<MeiliSearchProvider> logger)
    {
        this._client = client;
        this._indexPrefix = indexPrefix;
        this._logger = logger;
    }

    public async Task<int> CreateOrUpdateIndexAsync(ISearchIndexDataProvider dataProvider, CancellationToken token = default)
    {
        var indexStartAt = DateTimeOffset.UtcNow;

        var indexName = $"{_indexPrefix.Prefix}{dataProvider.Name}";
        var index = _client.Index(indexName);
        
        var indexLastUpdatedAt = await GetLastUpdated(indexName);
        
        var fields = await dataProvider.GetFields().ToListAsync(token);
        
        var tasks = new TaskAwaiter(_client);

        var synonyms = await dataProvider.GetSynonyms().ToListAsync(token);
        
        tasks.AddTaskResponse(await index.UpdateSettingsAsync(new Settings()
        {
            DisplayedAttributes = fields.Where(a => a.IsRetrievable).Select(a => a.Name),
            SearchableAttributes = fields.Where(a => a.IsSearchable).Select(a => a.Name),
            FilterableAttributes = fields.Where(a => a.IsFilterable).Select(a => a.Name),
            SortableAttributes = fields.Where(a => a.IsSortable).Select(a => a.Name),
            Synonyms = synonyms.SelectMany(GetSynonyms)
                .GroupBy(a => a.Key)
                .ToDictionary(a => a.Key, a => a.Select(b => b.Value)),
            // RankingRules = [ "typo", "words", "proximity", "attribute", "wordsPosition", "exactness" ],
            Faceting = new Faceting()
            {
                MaxValuesPerFacet = 1000
            }
        }, token));

        await tasks.WaitForTasks(token);
        
        int updated = 0, deleted = 0;
        await foreach (var data in dataProvider.Get(indexLastUpdatedAt).Buffer(500).WithCancellation(token))
        {
            updated += data.Count;

            var documents = string.Join("\n", data.Select(a =>
                JsonSerializer.Serialize(a, a.GetType(), _jsonOptionsCamelCaseIgnoreWritingNull)));
            
            tasks.AddTaskResponse(
                await index
                    .AddDocumentsNdjsonAsync(documents, "id", token)
            );
        }
        
        _logger.LogInformation("MeiliSearch {index} updated with {count}", indexName, updated);

        if (indexLastUpdatedAt is { } lastUpdate)
        {
            await foreach (var data in dataProvider.GetDeleted(lastUpdate).Buffer(24).WithCancellation(token))
            {
                deleted += data.Count;
                
                tasks.AddTaskResponse(await index.DeleteDocumentsAsync(data, token));
            }
        }
        
        _logger.LogInformation("MeiliSearch {index} deleted with {deleted}", indexName, deleted);

        await tasks.WaitForTasks(token);
        await SetLastUpdated(indexName, indexStartAt);

        return updated + deleted;
    }

    private record Metadata(string Id, long UpdatedAt);
    
    private async Task<DateTimeOffset?> GetLastUpdated(string indexName)
    {
        try
        {
            var metaIndex = _client.Index($"{_indexPrefix.Prefix}MetaCollections");
            var data = await metaIndex.GetDocumentAsync<Metadata>(indexName);
            return data is null ? null : DateTimeOffset.FromUnixTimeMilliseconds(data.UpdatedAt);
        } catch (MeilisearchApiError e) when (e.Code is "index_not_found" or "document_not_found") {
            return null;
        }
    }
    
    private static IEnumerable<KeyValuePair<string, string>> GetSynonyms(ISynonym synonym)
    {
        if (synonym is IOneWaySynonym oneWay)
        {
            foreach (var s in oneWay.Synonyms)
            {
                yield return new KeyValuePair<string, string>(oneWay.Root, s);
            }
        }
        else
        {
            foreach (var s in synonym.Synonyms)
            foreach (var b in synonym.Synonyms)
            {
                if (s != b)
                {
                    yield return new KeyValuePair<string, string>(s, b);
                }
            }
        }
    }
    
    private async Task SetLastUpdated(string indexName, DateTimeOffset? updatedAt)
    {
        var metaIndex = _client.Index($"{_indexPrefix.Prefix}MetaCollections");
        
        TaskInfo task;
        if (updatedAt is null)
        {
            task = await metaIndex.DeleteOneDocumentAsync(indexName);
        }
        else
        {
            task = await metaIndex.AddDocumentsAsync([new Metadata(indexName, updatedAt.Value.ToUnixTimeMilliseconds())], "id");
        }
        
        await TaskAwaiter.WaitForSingleTask(_client, task.TaskUid);
    }
    
    private class DictJsonConverter : JsonConverter<IReadOnlyDictionary<string, object>>
    {
        public override Dictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, JsonSerializerOptions.Default);
        }

        public override void Write(Utf8JsonWriter writer, IReadOnlyDictionary<string, object> value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, JsonSerializerOptions.Default);
            foreach (var (key, _value) in value)
            {
                writer.WritePropertyName("attributes." + key);
                JsonSerializer.Serialize(writer, _value, options);
            }
        }
    }
}