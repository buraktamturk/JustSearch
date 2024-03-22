using JustSearch.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace JustSearch.Tests;

public class JustSearchTests
{
    [Fact]
    public void TestServiceCollectionBuild()
    {
        var serviceCollection = new ServiceCollection();
        
        serviceCollection.AddJustSearch();

        var serviceProvider = serviceCollection.BuildServiceProvider(true);

        var opts = serviceProvider.GetRequiredService<JustSearchOptions>();
        Assert.NotNull(opts);
        
        var trigger = serviceProvider.GetRequiredService<ISearchIndexTrigger>();
        Assert.NotNull(trigger);
    }
}