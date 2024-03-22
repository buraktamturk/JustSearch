using System.Globalization;
using Algolia.Search.Clients;
using Algolia.Search.Models.Batch;
using Algolia.Search.Models.Common;
using Algolia.Search.Models.Enums;
using Algolia.Search.Models.Rules;
using Algolia.Search.Models.Settings;
using JustSearch.Abstractions;
using Microsoft.Extensions.Logging;

namespace JustSearch.Algolia;

public sealed class AlgoliaProvider : ISearchIndexProvider
{
    public string Name => "Algolia";

    private readonly ISearchClient _searchClient;
    private readonly IIndexPrefix _prefix;
    private readonly ILogger _logger;

    private readonly IReadOnlyCollection<string> ordering
        = new[] { "asc", "desc" };
    
    public AlgoliaProvider(ISearchClient searchClient, IIndexPrefix prefix, ILogger<AlgoliaProvider> logger)
    {
        this._logger = logger;
        this._prefix = prefix;
        this._searchClient = searchClient;
    }
    
    public async Task<int> CreateOrUpdateIndexAsync(ISearchIndexDataProvider dataProvider, CancellationToken token = default)
    {
        var indexStartAt = DateTimeOffset.UtcNow;

        var env = _prefix.Prefix ?? "";
        var totalCount = 0;

        var taskIds = new List<(SearchIndex, long)>();
        var indexName = $"{env}{dataProvider.Name}De";
        var index = _searchClient.InitIndex(indexName);
        
        var fields = await dataProvider.GetFields().ToListAsync(token);
        var searchableAttributes = fields.Where(a => a.IsSearchable).Select(a => a.Name).ToList();
        var facets = fields.Where(a => a.IsFacet || a.IsFilterable)
            .Select(a => a.IsFilterable ? $"filterOnly({a.Name})" : a.Name).ToList();
        var unRetrievable = fields.Where(a => !a.IsRetrievable).Select(a => a.Name).ToList();
        var sortKeys = fields.Where(a => a.IsSortable).Select(a => a.Name).ToList();

        if (!await index.ExistsAsync(CancellationToken.None)) {
            _logger.LogInformation("Algolia index does not exist, creating");
            
            var res = await index.SetSettingsAsync(new IndexSettings
            {
                SearchableAttributes = searchableAttributes,
                AttributesForFaceting = facets,
                MaxValuesPerFacet = 1000,
                IndexLanguages = new List<string>() {"de"},
                QueryLanguages = new List<string>() {"de","tr"}
            });

            await index.WaitTaskAsync(res.TaskID, ct: token);
        }
        
        DateTimeOffset? indexLastUpdatedAt = null;
        
        var settings = await index.GetSettingsAsync(ct: CancellationToken.None);
        if (settings.UserData is string userData && DateTimeOffset.TryParse(userData, CultureInfo.InvariantCulture, out var _indexLastUpdatedAt))
        {
            indexLastUpdatedAt = _indexLastUpdatedAt;
            _logger.LogInformation("Algolia {index} last updated at {indexLastUpdatedAt}.", indexName, indexLastUpdatedAt);

            var items = await index.BrowseFromAsync<ISearchable>(new BrowseIndexQuery()
            {
                Length = 1,
                AttributesToRetrieve = new[] { "objectID" }
            }, ct: token);
            if (items.NbHits == 0)
            {
                _logger.LogInformation("Algolia {index} is empty.", indexName);
                indexLastUpdatedAt = null;
            }
        }
        else
        {
            _logger.LogInformation("Algolia {index} does not exist.", indexName);
        }

        var count = 0;
        await foreach (var data in dataProvider.Get(indexLastUpdatedAt).Buffer(500).WithCancellation(token))
        {
            count += data.Count;
            await index.BatchAsync(
                new BatchRequest<ISearchable>(BatchActionType.UpdateObject, data),
                ct: token
            );
        }
        
        _logger.LogInformation("Algolia {index} updated with {count}", indexName, count);
        totalCount += count;

        var deleted = 0;
        if (indexLastUpdatedAt is { } lastUpdate)
        {
            await foreach (var data in dataProvider.GetDeleted(lastUpdate).Buffer(500).WithCancellation(token))
            {
                deleted += data.Count;
                await index.BatchAsync(
                    new BatchRequest<object>(BatchActionType.DeleteObject, data.Select(a => new { objectID = a })),
                    ct: token
                );
            }
        }
        
        _logger.LogInformation("Algolia {index} deleted with {deleted}", indexName, deleted);
        totalCount += deleted;
        
        settings = await index.GetSettingsAsync(ct: CancellationToken.None);
        settings.SearchableAttributes = searchableAttributes;
        settings.AttributesForFaceting = facets;
        settings.UnretrievableAttributes = unRetrievable;
        settings.RenderingContent = new RenderingContent()
        {
            FacetOrdering = new FacetOrdering()
            {
                Facets = new FacetsOrder()
                {
                    Order = facets.Where(a => !a.Contains('(')).ToList()
                }
            }
        };
        settings.UserData = indexStartAt.ToString(CultureInfo.InvariantCulture);

        if (sortKeys.Count > 0)
        {
            var newReplicas = sortKeys
                .SelectMany(k => ordering.Select(o => ($"{indexName}_{k}_{o}", k, o)))
                .ToList();

            if (settings.Replicas is null || !newReplicas.All(a => settings.Replicas.Contains(a.Item1)))
            {
                _logger.LogInformation("Processing replicas for {index}", indexName);
                settings.Replicas = newReplicas.Select(a => a.Item1).ToList();
            
                var indexTask = await index.SetSettingsAsync(settings, ct: CancellationToken.None);
                await index.WaitTaskAsync(indexTask.TaskID, ct: token);

                foreach (var (newIndexName, columnName, order) in newReplicas)
                {
                    _logger.LogInformation("Processing replica for {index}: {newIndexName}", indexName, newIndexName);
                    var newIndex = _searchClient.InitIndex(newIndexName);
                    var newTask = await newIndex.SetSettingsAsync(new IndexSettings()
                    {
                        SearchableAttributes = settings.SearchableAttributes,
                        AttributesForFaceting = settings.AttributesForFaceting,
                        UnretrievableAttributes = settings.UnretrievableAttributes,
                        RenderingContent = settings.RenderingContent,
                        Ranking = new List<string>()
                        {
                            $"{order}({columnName})"
                        }
                    }, ct: CancellationToken.None);
                    taskIds.Add((index, newTask.TaskID));
                }
            }
        }
        
        var task = await index.SetSettingsAsync(settings, ct: CancellationToken.None);
        taskIds.Add((index, task.TaskID));

        await Task.WhenAll(
            taskIds.Select(a => a.Item1.WaitTaskAsync(a.Item2, ct: token))
        );

        return totalCount;
    }
}