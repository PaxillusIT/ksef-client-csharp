using KSeF.Client.Api.Services;
using KSeF.Client.Core.Interfaces;
using KSeF.Client.Core.Models.Certificates;
using KSeF.Client.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Http.Headers;
namespace KSeF.Client.DI;

/// <summary>
/// Extension methods do rejestracji KSeF SDK w kontenerze DI.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Rejestruje wszystkie potrzebne serwisy do korzystania z KSeF
    /// </summary>
    /// <param name="services">Rozszerzany interfejs</param>
    /// <param name="configure">Opcje klienta KSeF</param>
    /// <param name="pemCertificatesFetcher">Delegat służacy do pobrania publicznych certyfikatów KSeF</param>
    /// <exception cref="ArgumentException"></exception>
    public static IServiceCollection AddKSeFClient(this IServiceCollection services,
        Action<KSeFClientOptions> configure,
        Func<IServiceProvider, CancellationToken, Task<ICollection<PemCertificateInfo>>> pemCertificatesFetcher = null)
    {
        var options = new KSeFClientOptions();
        configure(options);
        if (string.IsNullOrEmpty(options.BaseUrl))
            throw new ArgumentException("BaseUrl musi być poprawnym URL.", nameof(options.BaseUrl));

        services.AddSingleton(options);

        services
            .AddHttpClient<IRestClient, RestClient>(http =>
            {
                http.BaseAddress = new Uri(options.BaseUrl);
                if (options.CustomHeaders != null && options.CustomHeaders.Count > 0)
                {
                    foreach (var header in options.CustomHeaders)
                        http.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
                http.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                var handler = new HttpClientHandler();
                if (options.WebProxy != null)
                {
                    handler.Proxy = options.WebProxy;
                    handler.UseProxy = true;
                }
                return handler;
            });

        services.AddScoped<IKSeFClient, Http.KSeFClient>();
        services.AddScoped<IAuthCoordinator, AuthCoordinator>();
        services.AddHostedService<CryptographyWarmupHostedService>();
        services.TryAddSingleton<ICryptographyService>(serviceProvider =>
        {
            if (pemCertificatesFetcher != null)
            {
                return new CryptographyService(cancellationToken => pemCertificatesFetcher(serviceProvider, cancellationToken));
            }
            else
            {
                return new CryptographyService(async cancellationToken =>
                {
                    using var scope = serviceProvider.CreateScope();
                    var ksefCLient = scope.ServiceProvider.GetRequiredService<IKSeFClient>();
                    return await ksefCLient.GetPublicCertificatesAsync(cancellationToken);
                });
            }
        });

        services.AddScoped<ISignatureService, SignatureService>();
        services.AddScoped<IQrCodeService, QrCodeService>();
        services.AddSingleton<IPersonTokenService, PersonTokenService>();
        services.AddScoped<IVerificationLinkService, VerificationLinkService>();

        services.AddLocalization(options =>
        {
            options.ResourcesPath = "Resources";
        });

        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.SetDefaultCulture("pl-PL")
                .AddSupportedCultures("pl-PL", "en-US")
                .AddSupportedUICultures("pl-PL", "en-US");
        });

        return services;
    }
}
