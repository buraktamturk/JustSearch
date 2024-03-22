using System.Text.Json;
using System.Text.Json.Serialization;
using JustSearch.Abstractions;
using Microsoft.Extensions.Logging;
using Typesense;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace JustSearch.Typesense;

public sealed class TypesenseProvider : ISearchIndexProvider
{
    public string Name => "TypeSense";

    private readonly IIndexPrefix _indexPrefix;
    private readonly ITypesenseClient _typesenseClient;
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

    public TypesenseProvider(IIndexPrefix indexPrefix, ITypesenseClient typesenseClient, ILogger<TypesenseProvider> logger)
    {
        this._indexPrefix = indexPrefix;
        this._logger = logger;
        this._typesenseClient = typesenseClient;
    }
    
    public async Task<int> CreateOrUpdateIndexAsync(ISearchIndexDataProvider dataProvider, CancellationToken token = default)
    {
        var indexStartAt = DateTimeOffset.UtcNow;

        var env = _indexPrefix.Prefix ?? "";
        var totalCount = 0;

        var indexName = $"{env}{dataProvider.Name}";
        
        var fields = await dataProvider.GetFields().ToListAsync(token);
        var collection = await GetOrCreateCollection(indexName, fields);

        var indexLastUpdatedAt = await GetLastUpdated(collection.Name);
        
        var count = 0;
        await foreach (var data in dataProvider.Get(indexLastUpdatedAt).Buffer(500).WithCancellation(token))
        {
            count += data.Count;
            
            var response = await _typesenseClient.ImportDocuments<ISearchable>(collection.Name, data.Select(a => JsonSerializer.Serialize(a, a.GetType(), _jsonOptionsCamelCaseIgnoreWritingNull)), data.Count, ImportType.Upsert);

            var errors = response.Where(a => a.Error is not null);
            if (errors.Any())
            {
                _logger.LogError("TypeSense {index} import error: {errors}", indexName, string.Join("\n", errors));
            }
        }
        
        _logger.LogInformation("TypeSense {index} updated with {count}", indexName, count);
        totalCount += count;

        var deleted = 0;
        if (indexLastUpdatedAt is { } lastUpdate)
        {
            await foreach (var data in dataProvider.GetDeleted(lastUpdate).Buffer(24).WithCancellation(token))
            {
                deleted += data.Count;
                
                await _typesenseClient.DeleteDocuments(collection.Name, $"filter_id=id: [{string.Join(",", data)}]");
            }
        }
        
        _logger.LogInformation("TypeSense {index} deleted with {deleted}", indexName, deleted);
        totalCount += deleted;

        await SetLastUpdated(collection.Name, indexStartAt);

        return totalCount;
    }

    private async Task<DateTimeOffset?> GetLastUpdated(string collectionName)
    {
        try {
            var aliases = await _typesenseClient.ListCollectionAliases();
            return aliases.CollectionAliases
                .Where(a => a.Name.StartsWith(collectionName))
                .Select(alias => (DateTimeOffset?)DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(alias.Name[(alias.Name.LastIndexOf('-') + 1)..])))
                .DefaultIfEmpty()
                .Max();
        } catch (TypesenseApiNotFoundException) {
            return null;
        }
    }
    
    private async Task SetLastUpdated(string collectionName, DateTimeOffset lastUpdated)
    {
        try
        {
            await DeleteAliases(collectionName);
        } catch (TypesenseApiNotFoundException) {
            
        }

        var newName = $"{collectionName}-{lastUpdated.ToUnixTimeMilliseconds()}";
        await _typesenseClient.UpsertCollectionAlias(newName, new CollectionAlias(collectionName));
    }

    private async Task DeleteAliases(string collectionName)
    {
        var aliases = await _typesenseClient.ListCollectionAliases();
        foreach (var alias in aliases.CollectionAliases.Where(a => a.Name.StartsWith(collectionName)))
        {
            await _typesenseClient.DeleteCollectionAlias(alias.Name);
        }
    }

    private async Task<CollectionResponse> GetOrCreateCollection(string name, IReadOnlyCollection<ISearchField> fields)
    {
        var _fields = fields.Select(a => new Field(a.Name, a.IsNumber ? FieldType.Float : FieldType.Auto, a.IsFacet,
            !a.IsSortable, a.IsFacet || a.IsFilterable || a.IsSearchable || a.IsSortable, a.IsSortable))
            .ToList();
        
        CollectionResponse collection;
        try
        {
            collection = await _typesenseClient.RetrieveCollection(name);
            if (!IsEqual(collection.Fields, _fields))
            {
                _logger.LogInformation("TypeSense {index} fields changed, deleting and recreating collection", name);
                await DeleteAliases(name);
                await _typesenseClient.DeleteCollection(name);
                collection = await _typesenseClient.CreateCollection(new Schema(
                    name,
                    _fields
                )
                {
                    DefaultSortingField = fields.FirstOrDefault(a => a.IsSortable)?.Name,
                });
            }
        }
        catch (TypesenseApiNotFoundException)
        {
            collection = await _typesenseClient.CreateCollection(new Schema(
                name,
                _fields
            )
            {
                DefaultSortingField = fields.FirstOrDefault(a => a.IsSortable)?.Name,
            });
        }

        return collection;
    }
    
    private static bool IsEqual(IReadOnlyCollection<Field> a, IReadOnlyCollection<Field> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var field in a)
        {
            if (!b.Any(c => c.Name == field.Name && c.Type == field.Type && c.Facet == field.Facet && c.Index == field.Index && c.Sort == field.Sort))
            {
                return false;
            }
        }

        return true;
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
