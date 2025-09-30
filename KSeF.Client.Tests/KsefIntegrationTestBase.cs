using KSeF.Client.Api.Services;
using KSeF.Client.Core.Interfaces;
using KSeF.Client.DI;
using KSeF.Client.Tests.Config;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Text.Json;

namespace KSeF.Client.Tests;

[Collection("KsefIntegration")]
public abstract class KsefIntegrationTestBase : IDisposable
{
    internal const int SleepTime = 500;

    private ServiceProvider _serviceProvider = default!;
    private IServiceScope _scope = default!;

    protected IKSeFClient KsefClient => _scope.ServiceProvider.GetRequiredService<IKSeFClient>();
    protected ISignatureService SignatureService => _scope.ServiceProvider.GetRequiredService<ISignatureService>();
    protected IPersonTokenService TokenService => _scope.ServiceProvider.GetRequiredService<IPersonTokenService>();
    protected ICryptographyService CryptographyService => _scope.ServiceProvider.GetRequiredService<ICryptographyService>();
    protected IQrCodeService QRCodeService => _scope.ServiceProvider.GetRequiredService<IQrCodeService>();
    protected IVerificationLinkService VerificationLinkService => _scope.ServiceProvider.GetRequiredService<IVerificationLinkService>();

    public KsefIntegrationTestBase()
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
        _serviceProvider.Dispose();
    }
}
