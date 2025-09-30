using KSeF.Client.Core.Interfaces;
using KSeF.Client.DI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KSeF.Client.Api.Services;

public sealed class CryptographyWarmupHostedService : IHostedService
{
    private readonly ICryptographyService _cryptographyService;
    private readonly KSeFClientOptions _kSeFClientOptions;

    public CryptographyWarmupHostedService(
        ICryptographyService crypto,
        ILogger<CryptographyWarmupHostedService> logger,
        KSeFClientOptions kSeFClientOptions)
    {
        _cryptographyService = crypto;
        _kSeFClientOptions = kSeFClientOptions;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        switch (_kSeFClientOptions.WarmupOnStart)
        {
            case WarmupMode.Disabled:
                return Task.CompletedTask;
            case WarmupMode.NonBlocking:
                _ = Task.Run(() => SafeWarmup(cancellationToken), CancellationToken.None);
                return Task.CompletedTask;
            case WarmupMode.Blocking:
                return SafeWarmup(cancellationToken);
            default:
                return Task.CompletedTask;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SafeWarmup(CancellationToken cancellationToken)
    {
        try
        {
            await _cryptographyService.WarmupAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            if (_kSeFClientOptions.WarmupOnStart == WarmupMode.Blocking)
            {
                throw;
            }
        }
    }
}
