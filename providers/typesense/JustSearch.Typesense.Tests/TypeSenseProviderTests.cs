using JustSearch.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Typesense;
using Typesense.Setup;
using Xunit;

namespace JustSearch.Typesense.Tests;

public class TypeSenseProviderTests : IDisposable
{
    private readonly Mock<ISearchIndexDataProvider> _dataProviderMock;
    private readonly HttpClient _httpClient;
    private readonly ITypesenseClient _typesenseClient;
    private readonly TypesenseProvider _provider;
    
    public TypeSenseProviderTests()
    {
        _httpClient = new HttpClient();

        var optionsMock = new Mock<IOptions<Config>>(MockBehavior.Strict);
        optionsMock.Setup(a => a.Value)
            .Returns(new Config([
                new Node("localhost", "8108")
            ],
            ""));

        var indexPrefixMock = new Mock<IIndexPrefix>(MockBehavior.Strict);
        indexPrefixMock.Setup(a => a.Prefix)
            .Returns("Demo_");
        
        _typesenseClient = new TypesenseClient(optionsMock.Object, _httpClient);
        
        _provider = new TypesenseProvider(
            indexPrefixMock.Object, 
            _typesenseClient,
            NullLogger<TypesenseProvider>.Instance
        );

        _dataProviderMock = new Mock<ISearchIndexDataProvider>(MockBehavior.Strict);
    }
    
    [Fact]
    public async Task TestTypesenseProvider()
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
        var aliases = await _typesenseClient.ListCollectionAliases();
        foreach (var alias in aliases.CollectionAliases.Where(a => a.CollectionName.StartsWith("Demo_")))
        {
            await _typesenseClient.DeleteCollectionAlias(alias.Name);
        }

        var collections = await _typesenseClient.RetrieveCollections();
        foreach (var collection in collections.Where(a => a.Name.StartsWith("Demo_")))
        {
            await _typesenseClient.DeleteCollection(collection.Name);
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private record SampleData(string Id, string Name, string Url, decimal Price)
        : ISearchable;
}
