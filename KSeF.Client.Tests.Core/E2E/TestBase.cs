using KSeF.Client.Api.Services;
using KSeF.Client.Core.Interfaces;
using KSeF.Client.Tests.Core.Config;
using KSeF.Client.DI;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace KSeF.Client.Tests.Core.E2E;
public abstract class TestBase : IDisposable
{
    internal const int SleepTime = 2000;

    private IServiceScope _scope = default!;
    private ServiceProvider _serviceProvider = default!;

    protected static readonly CancellationToken CancellationToken = CancellationToken.None;
    protected IKSeFClient KsefClient => _scope.ServiceProvider.GetRequiredService<IKSeFClient>();
    protected ISignatureService SignatureService => _scope.ServiceProvider.GetRequiredService<ISignatureService>();
    protected IPersonTokenService TokenService => _scope.ServiceProvider.GetRequiredService<IPersonTokenService>();
    protected ICryptographyService CryptographyService => _scope.ServiceProvider.GetRequiredService<ICryptographyService>();

    
    public TestBase()
    {
        ServiceCollection services = new ServiceCollection();

        ApiSettings apiSettings = TestConfig.GetApiSettings();

        var customHeadersFromSettings = TestConfig.Load()["ApiSettings:customHeaders"];
        if (!string.IsNullOrEmpty(customHeadersFromSettings))
        {
            apiSettings.CustomHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(customHeadersFromSettings);
        }

        services.AddKSeFClient(options =>
        {
            options.BaseUrl = apiSettings.BaseUrl!;
            options.CustomHeaders = apiSettings.CustomHeaders ?? new Dictionary<string, string>();
            options.WarmupOnStart = WarmupMode.Disabled;
        });

        _serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        _scope = _serviceProvider.CreateScope();

        // opcjonalne: inicjalizacja lub inne czynności startowe
        _scope.ServiceProvider.GetRequiredService<ICryptographyService>()
                           .WarmupAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _scope.Dispose();
        _serviceProvider?.Dispose();
    }
}
