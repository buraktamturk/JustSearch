using JustSearch.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JustSearch;

internal sealed class JustSearchBackgroundService : BackgroundService
{
    private readonly JustSearchOptions _options;
    private readonly SearchIndexJobChannel _dataProviderChannel;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<ISearchIndexProvider> _providers;
    private readonly ILogger _logger;
    
    public JustSearchBackgroundService(JustSearchOptions options, SearchIndexJobChannel dataProviderChannel, IServiceProvider serviceProvider, IEnumerable<ISearchIndexProvider> providers, ILogger<JustSearchBackgroundService> logger)
    {
        _options = options;
        _dataProviderChannel = dataProviderChannel;
        _serviceProvider = serviceProvider;
        _providers = providers;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("JustSearch is running on background.");

        if (_options.SyncOnStartup)
        {
            _logger.LogInformation("Running JustSearch on startup.");
            await Process(null, stoppingToken);
        }
        
        await foreach (var (dataProviders, tsc) in _dataProviderChannel.Reader.ReadAllAsync(stoppingToken))
        {
            _logger.LogInformation("Running JustSearch on trigger.");
            try {
                await Process(dataProviders, stoppingToken);
                tsc.SetResult();
            } catch (OperationCanceledException) {
                tsc.SetCanceled(stoppingToken);
            } catch (Exception e) {
                _logger.LogError(e, "Error running JustSearch on trigger.");
                tsc.SetException(e);
            }
        }
    }

    private async Task Process(IEnumerable<Func<IServiceScope, ISearchIndexDataProvider>>? dataProviders = null, CancellationToken token = default)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var _dataProviders = 
            dataProviders is null ? scope.ServiceProvider.GetRequiredService<IEnumerable<ISearchIndexDataProvider>>() :
            dataProviders.Select(type => type.Invoke(scope));
        
        foreach (var provider in _providers)
        {
            foreach (var dataProvider in _dataProviders)
            {
                _logger.LogInformation("Running {providerName} for {dataProviderName}.", provider.Name, dataProvider.Name);
                try {
                    await provider.CreateOrUpdateIndexAsync(dataProvider, token);
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    _logger.LogError(e, "Error running {providerName} for {dataProviderName}.", provider.Name, dataProvider.Name);
                    throw;
                }
            }
        }
    }
}