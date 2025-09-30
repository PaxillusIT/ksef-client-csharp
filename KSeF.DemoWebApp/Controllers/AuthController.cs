using KSeF.Client;
using KSeF.Client.Api.Builders.Auth;
using KSeF.Client.Api.Builders.X509Certificates;
using KSeF.Client.Core.Interfaces;
using KSeF.Client.Core.Models;
using KSeF.Client.Core.Models.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using WebApplication.Services;

namespace WebApplication.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{

    private readonly IAuthCoordinator _authCoordinator;
    private readonly IKSeFClient _ksefClient;
    private readonly ISignatureService _signatureService;


    private readonly string _contextIdentifier;
    private readonly string? xMLDirectory;

    public AuthController(IAuthCoordinator authCoordinator, IConfiguration configuration, IKSeFClient ksefClient, ISignatureService signatureService)
    {
        _authCoordinator = authCoordinator;
        _ksefClient = ksefClient;
        _signatureService = signatureService;
        _contextIdentifier = configuration["Tools:contextIdentifier"]!;
        xMLDirectory = configuration["Tools:XMLDirectory"];
    }

    [HttpPost("auth-by-coordinator-with-PZ")]
    public async Task<ActionResult<AuthOperationStatusResponse>> AuthWithPZAsync(string contextIdentifier, CancellationToken cancellationToken)
    {
        // Inicjalizacja przykłdowego identyfikatora - w tym przypadku NIP.

        return await _authCoordinator.AuthAsync(
                                                    ContextIdentifierType.Nip,
                                                    !string.IsNullOrWhiteSpace(contextIdentifier) ? contextIdentifier : _contextIdentifier,
                                                    SubjectIdentifierTypeEnum.CertificateSubject,
                                                    xmlSigner: (xml) => { return XadeSDummy.SignWithPZ(xml, xMLDirectory); },
                                                    authorizationPolicy: null,
                                                    cancellationToken);
    }

    [HttpPost("auth-step-by-step")]
    public async Task<ActionResult<AuthOperationStatusResponse>> AuthStepByStepAsync(string contextIdentifier, CancellationToken cancellationToken)
    {

        return await _ksefClient
            .AuthSessionStepByStep(
            SubjectIdentifierTypeEnum.CertificateSubject,
            !string.IsNullOrWhiteSpace(contextIdentifier) ? contextIdentifier : _contextIdentifier,
            (xml) => { return XadeSDummy.SignWithPZ(xml, xMLDirectory); },
            authorizationPolicy: null,
            cancellationToken);
    }

    [HttpGet("refresh-token")]
    public async Task<ActionResult<RefreshTokenResponse>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        return await _ksefClient.RefreshAccessTokenAsync(
            refreshToken,
            cancellationToken);
    }

    [HttpGet("auth-with-ksef-certificate")]
    public async Task<AuthOperationStatusResponse> AuthWithKsefCert(string certInBase64, string contextIdentifier, string privateKey, [FromServices] ISignatureService signatureService, CancellationToken cancellationToken)
    {
        var cert = Convert.FromBase64String(certInBase64);
        var x509 = new X509Certificate2(cert);
        var privateKeyBytes = Convert.FromBase64String(privateKey);

        using var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(privateKeyBytes, out _);

        var challengeResponse = await _ksefClient
           .GetAuthChallengeAsync(cancellationToken);

        var challenge = challengeResponse.Challenge;

        var authTokenRequest =
            AuthTokenRequestBuilder
            .Create()
            .WithChallenge(challenge)
            .WithContext(ContextIdentifierType.Nip, contextIdentifier)
            .WithIdentifierType(SubjectIdentifierTypeEnum.CertificateSubject);


        AuthTokenRequest authorizeRequest = authTokenRequest.Build();

        var unsignedXml = AuthTokenRequestSerializer.SerializeToXmlString(authorizeRequest);

        var signedXml = signatureService.Sign(unsignedXml, x509.CopyWithPrivateKey(rsa));

        var authSubmission = await _ksefClient
           .SubmitXadesAuthRequestAsync(signedXml, false, cancellationToken);

        AuthStatus authStatus;
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromMinutes(2);

        do
        {
            authStatus = await _ksefClient.GetAuthStatusAsync(authSubmission.ReferenceNumber, authSubmission.AuthenticationToken.Token, cancellationToken);

            Console.WriteLine(
                $"Polling: StatusCode={authStatus.Status.Code}, " +
                $"Description='{authStatus.Status.Description}', " +
                $"Elapsed={DateTime.UtcNow - startTime:mm\\:ss}");

            if (authStatus.Status.Code != 200 && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
        while (authStatus.Status.Code != 200
            && !cancellationToken.IsCancellationRequested
            && (DateTime.UtcNow - startTime) < timeout);

        if (authStatus.Status.Code != 200)
        {
            Console.WriteLine("Timeout: Brak tokena po 2 minutach.");
            throw new Exception("Timeout Uwierzytelniania: Brak tokena po 2 minutach.");
        }
        var accessTokenResponse = await _ksefClient.GetAccessTokenAsync(authSubmission.AuthenticationToken.Token, cancellationToken);

        // 7) Zwróć token            
        return accessTokenResponse;
    }

    [HttpPost("access-token")]
    public async Task<AuthOperationStatusResponse> GetAuthOperationStatusAsync([FromBody] CertificateRequestModel certificateRequestModel)
    {
        AuthChallengeResponse challengeResponse = await _ksefClient.GetAuthChallengeAsync();

        AuthTokenRequest authTokenRequest = AuthTokenRequestBuilder
            .Create()
            .WithChallenge(challengeResponse.Challenge)
            .WithContext(ContextIdentifierType.Nip, certificateRequestModel.ContextIdentifier.Value)
            .WithIdentifierType(SubjectIdentifierTypeEnum.CertificateSubject)
            .Build();

        string unsignedXml = AuthTokenRequestSerializer.SerializeToXmlString(authTokenRequest);

        System.Security.Cryptography.X509Certificates.X509Certificate2 certificate = SelfSignedCertificateForSignatureBuilder
                .Create()
                .WithGivenName(certificateRequestModel.GivenName)
                .WithSurname(certificateRequestModel.Surname)
                .WithSerialNumber($"{certificateRequestModel.SerialNumberPrefix}-{certificateRequestModel.SerialNumber}")
                .WithCommonName(certificateRequestModel.CommonName)
                .Build();

        string signedXml = _signatureService.Sign(unsignedXml, certificate);
        SignatureResponse signatureResponse = await _ksefClient.SubmitXadesAuthRequestAsync(signedXml, false, CancellationToken.None);

        AuthStatus authStatus;
        DateTime startTime = DateTime.UtcNow;
        TimeSpan timeout = TimeSpan.FromMinutes(2);

        do
        {
            authStatus = await _ksefClient
                .GetAuthStatusAsync(signatureResponse.ReferenceNumber, signatureResponse.AuthenticationToken.Token);

            if (authStatus.Status.Code != 200)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
        while (authStatus.Status.Code == 100
               && (DateTime.UtcNow - startTime) < timeout);

        if (authStatus.Status.Code != 200)
        {
            string msg = $"Uwierzytelnienie nie powiodÂło się. Kod statusu: {authStatus?.Status.Code}, opis: {authStatus?.Status.Description}.";

            throw new Exception(msg);
        }

        AuthOperationStatusResponse authResult = await _ksefClient.GetAccessTokenAsync(signatureResponse.AuthenticationToken.Token);
        return authResult;
    }
}

public class CertificateRequestModel
{
    public ContextIdentifier ContextIdentifier { get; set; }
    public string GivenName { get; set; }
    public string Surname { get; set; }
    public string SerialNumberPrefix { get; set; }
    public string SerialNumber { get; set; }
    public string CommonName { get; set; }
}

public static class AuthSessionStepByStepHelper
{
    public static async Task<AuthOperationStatusResponse>
        AuthSessionStepByStep(this IKSeFClient ksefClient, SubjectIdentifierTypeEnum authIdentifierType, string contextIdentifier, Func<string, Task<string>> xmlSigner, AuthorizationPolicy? authorizationPolicy = null, CancellationToken cancellationToken = default)
    {

        // Wykonanie auth challenge.
        var challengeResponse = await ksefClient
            .GetAuthChallengeAsync();

        Console.WriteLine(challengeResponse.Challenge);

        // Wymagany jest podpis cyfrowy w formacie XAdES-BES.
        var authTokenRequest =
            AuthTokenRequestBuilder
            .Create()
            .WithChallenge(challengeResponse.Challenge)
            .WithContext(ContextIdentifierType.Nip, contextIdentifier)
            .WithIdentifierType(authIdentifierType)
            .WithAuthorizationPolicy(authorizationPolicy ?? new AuthorizationPolicy { /* ... */ })      // optional
            .Build();

        var unsignedXml = AuthTokenRequestSerializer.SerializeToXmlString(authTokenRequest);

        // TODO Trzeba podpisac XML przed wysłaniem
        var signedXml = await xmlSigner.Invoke(unsignedXml);

        // Przesłanie podpisanego XML do systemu KSeF
        var authOperationInfo = await ksefClient.
            SubmitXadesAuthRequestAsync(signedXml, false, cancellationToken);

        AuthStatus authorizationStatus;
        int maxRetry = 5;
        int currentLoginAttempt = 0;
        TimeSpan sleepTime = TimeSpan.FromSeconds(1);

        do
        {
            if (currentLoginAttempt >= maxRetry)
            {
                throw new Exception("Autoryzacja nieudana - przekroczono liczbę dozwolonych prób logowania.");
            }

            await Task.Delay(sleepTime + TimeSpan.FromSeconds(currentLoginAttempt));
            authorizationStatus = await ksefClient.GetAuthStatusAsync(
                authOperationInfo.ReferenceNumber,
                authOperationInfo.AuthenticationToken.Token);
            currentLoginAttempt++;
        }
        while (authorizationStatus.Status.Code != 200);

        // Uzyskanie accessToken w celu uwierzytelniania 
        var accessTokenResult = await ksefClient
            .GetAccessTokenAsync(authOperationInfo.AuthenticationToken.Token, cancellationToken);

        return accessTokenResult;
    }
}