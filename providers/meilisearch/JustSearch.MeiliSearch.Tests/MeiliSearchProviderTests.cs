using JustSearch.Abstractions;
using Meilisearch;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace JustSearch.MeiliSearch.Tests;

public class MeiliSearchProviderTests
{
    private readonly Mock<ISearchIndexDataProvider> _dataProviderMock;
    private readonly MeilisearchClient _client;
    private readonly MeiliSearchProvider _provider;
    
    public MeiliSearchProviderTests()
    {
        var indexPrefixMock = new Mock<IIndexPrefix>(MockBehavior.Strict);
        indexPrefixMock.Setup(a => a.Prefix)
            .Returns("Demo_");
        
        _client = new MeilisearchClient(
            "http://localhost:7700",
            "masterKey"
        );
        
        _provider = new MeiliSearchProvider(
            _client, 
            indexPrefixMock.Object,
            NullLogger<MeiliSearchProvider>.Instance
        );

        _dataProviderMock = new Mock<ISearchIndexDataProvider>(MockBehavior.Strict);
    }
    
    [Fact]
    public async Task TestMeiliSearchProvider()
    {
        await Clean();
        
        _dataProviderMock.Setup(a => a.GetSynonyms())
            .Returns(new ISynonym[]
            {
                new Synonym("test", ["mw1", "mw1"]),
                new OneWaySynonym("test2", "ow1", ["ow2", "ow3"]),
            }.ToAsyncEnumerable());
        
        _dataProviderMock
            .Setup(a => a.Name)
            .Returns("Test1");
        
        _dataProviderMock
            .Setup(a => a.GetFields())
            .Returns(new ISearchField[]
            {
                new SearchField("name", SearchFieldType.String, IsSearchable: true, Locale: "de"),
                new SearchField("url", SearchFieldType.String),
                new SearchField("price", SearchFieldType.Float),
            }.ToAsyncEnumerable());

        _dataProviderMock
            .Setup(a => a.Get(It.Is<DateTimeOffset?>(b => b == null)))
            .Returns(new SampleData[]
            {
                new("1", "Test Product", "https://example.com", 100),
                new("2", "Test Product 2", "https://example.com/2", 200),
            }.ToAsyncEnumerable());
        
       var affectedRows = await _provider.CreateOrUpdateIndexAsync(
            _dataProviderMock.Object,
            CancellationToken.None
        );
       
       Assert.Equal(2, affectedRows);
       
       _dataProviderMock.Setup(a => a.GetSynonyms())
           .Returns(new ISynonym[]
           {
               new Synonym("test3", ["mw2", "mw3"]),
               new OneWaySynonym("test2", "ow1", ["ow2", "ow3"]),
           }.ToAsyncEnumerable());
       
       _dataProviderMock
           .Verify(a => a.GetDeleted(It.IsAny<DateTimeOffset>()), Times.Never);
       
       _dataProviderMock
           .Setup(a => a.Get(It.IsNotNull<DateTimeOffset?>()))
           .Returns(new SampleData[]
           {
               new("3", "Test Product 3", "https://example.com/3", 100),
           }.ToAsyncEnumerable());
       
       _dataProviderMock
           .Setup(a => a.GetDeleted(It.IsAny<DateTimeOffset>()))
           .Returns(new[]
           {
               "1",
               "2"
           }.ToAsyncEnumerable());

       affectedRows = await _provider.CreateOrUpdateIndexAsync(
           _dataProviderMock.Object,
           CancellationToken.None
       );
       
       Assert.Equal(3, affectedRows);
    }
    
    private async Task Clean()
    {
        var indexes = await _client.GetAllIndexesAsync();
        foreach (var index in indexes.Results.Where(a => a.Uid.StartsWith("Demo_")))
        {
            await _client.DeleteIndexAsync(index.Uid);
        }
    }

    private record SampleData(string Id, string Name, string Url, decimal Price)
        : ISearchable;
}
