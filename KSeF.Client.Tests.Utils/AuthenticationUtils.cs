using KSeF.Client.Core.Interfaces;
using KSeF.Client.Core.Models.Authorization;
using KSeF.Client.Api.Builders.Auth;
using KSeF.Client.Core.Models;
using System.Security.Cryptography.X509Certificates;

namespace KSeF.Client.Tests.Utils;
public static class AuthenticationUtils
{
    /// <summary>
    /// Przeprowadza pełny proces uwierzytelnienia w KSeF z wykorzystaniem podpisu XAdES dla wskazanego NIP.
    /// </summary>
    /// <param name="ksefClient">Klient KSeF wykorzystywany do komunikacji z API.</param>
    /// <param name="signatureService">Usługa odpowiedzialna za podpisywanie XML (XAdES).</param>
    /// <param name="nip">Numer NIP, dla którego wykonywane jest uwierzytelnienie.</param>
    /// <param name="contextIdentifierType">Typ identyfikatora kontekstu (domyślnie Nip).</param>
    /// <returns><see cref="AuthOperationStatusResponse"/> zawierający access token i refresh token.</returns>
    /// <exception cref="Exception">Gdy uwierzytelnienie nie powiedzie się.</exception>
    public static async Task<AuthOperationStatusResponse> AuthenticateAsync(IKSeFClient ksefClient,
    ISignatureService signatureService,
        string nip,
        ContextIdentifierType contextIdentifierType = ContextIdentifierType.Nip)
    {
        AuthChallengeResponse challengeResponse = await ksefClient
            .GetAuthChallengeAsync();

        AuthTokenRequest authTokenRequest = GetAuthorizationTokenRequest(
            challengeResponse.Challenge,
            contextIdentifierType,
            nip,
            SubjectIdentifierTypeEnum.CertificateSubject);

        string unsignedXml = AuthTokenRequestSerializer.SerializeToXmlString(authTokenRequest);

        System.Security.Cryptography.X509Certificates.X509Certificate2 certificate =
            CertificateUtils.GetPersonalCertificate("A", "R", "TINPL", nip, "A R");

        string signedXml = signatureService.Sign(unsignedXml, certificate);

        SignatureResponse authOperationInfo = await ksefClient
          .SubmitXadesAuthRequestAsync(signedXml, false, CancellationToken.None);


        AuthStatus accessTokenResponse;
        DateTime startTime = DateTime.UtcNow;
        TimeSpan timeout = TimeSpan.FromMinutes(2);

        do
        {
            accessTokenResponse = await ksefClient
                .GetAuthStatusAsync(authOperationInfo.ReferenceNumber, authOperationInfo.AuthenticationToken.Token);

            Console.WriteLine(
                $"Odpytanie: KodStatusu={accessTokenResponse.Status.Code}, " +
                $"Opis='{accessTokenResponse.Status.Description}', " +
                $"Upłynęło={DateTime.UtcNow - startTime:mm\\:ss}");

            if (accessTokenResponse.Status.Code != 200)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
        while (accessTokenResponse.Status.Code == 100
               && (DateTime.UtcNow - startTime) < timeout);


        if (accessTokenResponse.Status.Code != 200)
        {
            string msg = $"Uwierzytelnienie nie powiodło się. Kod statusu: {accessTokenResponse?.Status.Code}, opis: {accessTokenResponse?.Status.Description}.";

            throw new Exception(msg);
        }

        AuthOperationStatusResponse authResult = await ksefClient.GetAccessTokenAsync(authOperationInfo.AuthenticationToken.Token);
        return authResult;
    }

    /// <summary>
    /// Przeprowadza proces uwierzytelnienia w KSeF, generując losowy NIP (na potrzeby testów) i wykorzystując podpis XAdES.
    /// </summary>
    /// <param name="ksefClient">Klient KSeF wykorzystywany do komunikacji z API.</param>
    /// <param name="signatureService">Usługa odpowiedzialna za podpisywanie XML (XAdES).</param>
    /// <param name="contextIdentifierType">Typ identyfikatora kontekstu (domyślnie Nip).</param>
    /// <returns><see cref="AuthOperationStatusResponse"/> zawierający access token i refresh token.</returns>
    /// <exception cref="Exception">Gdy uwierzytelnienie nie powiedzie się.</exception>
    public static async Task<AuthOperationStatusResponse> AuthenticateAsync(IKSeFClient ksefClient,
    ISignatureService signatureService,
        ContextIdentifierType contextIdentifierType = ContextIdentifierType.Nip)
    {
        string random = MiscellaneousUtils.GetRandomNip();
        string nip = random;

        AuthChallengeResponse challengeResponse = await ksefClient
            .GetAuthChallengeAsync();

        AuthTokenRequest authTokenRequest = GetAuthorizationTokenRequest(
            challengeResponse.Challenge,
            contextIdentifierType,
            nip,
            SubjectIdentifierTypeEnum.CertificateSubject);

        string unsignedXml = AuthTokenRequestSerializer.SerializeToXmlString(authTokenRequest);

        X509Certificate2 certificate = CertificateUtils.GetPersonalCertificate("A", "R", "TINPL", nip, "A R");

        string signedXml = signatureService.Sign(unsignedXml, certificate);

        SignatureResponse authOperationInfo = await ksefClient
          .SubmitXadesAuthRequestAsync(signedXml, false, CancellationToken.None);


        AuthStatus accessTokenResponse;
        DateTime startTime = DateTime.UtcNow;
        TimeSpan timeout = TimeSpan.FromMinutes(2);

        do
        {
            accessTokenResponse = await ksefClient
                .GetAuthStatusAsync(authOperationInfo.ReferenceNumber, authOperationInfo.AuthenticationToken.Token);

            Console.WriteLine(
                $"Odpytanie: KodStatusu={accessTokenResponse.Status.Code}, " +
                $"Opis='{accessTokenResponse.Status.Description}', " +
                $"Upłynęło={DateTime.UtcNow - startTime:mm\\:ss}");

            if (accessTokenResponse.Status.Code != 200)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
        while (accessTokenResponse.Status.Code == 100
               && (DateTime.UtcNow - startTime) < timeout);


        if (accessTokenResponse.Status.Code != 200)
        {
            string msg = $"Uwierzytelnienie nie powiodło się. Kod statusu: {accessTokenResponse?.Status.Code}, opis: {accessTokenResponse?.Status.Description}.";

            throw new Exception(msg);
        }

        AuthOperationStatusResponse authResult = await ksefClient.GetAccessTokenAsync(authOperationInfo.AuthenticationToken.Token);
        return authResult;
    }

    /// <summary>
    /// Buduje żądanie tokenu autoryzacyjnego (AuthTokenRequest) na podstawie otrzymanego wyzwania (challenge) i identyfikatora kontekstu.
    /// </summary>
    /// <param name="challengeToken">Token wyzwania otrzymany z KSeF.</param>
    /// <param name="contextIdentifierType">Typ identyfikatora kontekstu (np. Nip).</param>
    /// <param name="nip">Wartość identyfikatora kontekstu, zwykle NIP.</param>
    /// <param name="subjectIdentifierTypeEnum">Typ identyfikatora podmiotu (np. CertificateSubject).</param>
    /// <returns>Zbudowany <see cref="AuthTokenRequest"/> gotowy do serializacji i podpisu.</returns>
    public static AuthTokenRequest GetAuthorizationTokenRequest(
        string challengeToken,
        ContextIdentifierType contextIdentifierType,
        string nip,
        SubjectIdentifierTypeEnum subjectIdentifierTypeEnum = SubjectIdentifierTypeEnum.CertificateSubject)
    {
        AuthTokenRequest authTokenRequest = AuthTokenRequestBuilder
           .Create()
           .WithChallenge(challengeToken)
           .WithContext(contextIdentifierType, nip)
           .WithIdentifierType(subjectIdentifierTypeEnum)
           .WithAuthorizationPolicy(null)
           .Build();

        return authTokenRequest;
    }

    public static async Task<AuthOperationStatusResponse> AuthenticateAsync(IKSeFClient ksefClient,
        ISignatureService signatureService,
        string contextIdentifierValue,
        ContextIdentifierType contextIdentifierType,
        X509Certificate2 certificate,
        SubjectIdentifierTypeEnum subjectIdentifierType = SubjectIdentifierTypeEnum.CertificateSubject)
    {
        AuthChallengeResponse challengeResponse = await ksefClient
            .GetAuthChallengeAsync();

        AuthTokenRequest authTokenRequest = GetAuthorizationTokenRequest(
            challengeResponse.Challenge,
            contextIdentifierType,
            contextIdentifierValue,
            subjectIdentifierType);

        string unsignedXml = AuthTokenRequestSerializer.SerializeToXmlString(authTokenRequest);

        string signedXml = signatureService.Sign(unsignedXml, certificate);

        SignatureResponse authOperationInfo = await ksefClient
          .SubmitXadesAuthRequestAsync(signedXml, false, CancellationToken.None);


        AuthStatus accessTokenResponse;
        DateTime startTime = DateTime.UtcNow;
        TimeSpan timeout = TimeSpan.FromMinutes(2);

        do
        {
            accessTokenResponse = await ksefClient
                .GetAuthStatusAsync(authOperationInfo.ReferenceNumber, authOperationInfo.AuthenticationToken.Token);

            Console.WriteLine(
                $"Odpytanie: KodStatusu={accessTokenResponse.Status.Code}, " +
                $"Opis='{accessTokenResponse.Status.Description}', " +
                $"Upłynęło={DateTime.UtcNow - startTime:mm\\:ss}");

            if (accessTokenResponse.Status.Code != 200)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
        while (accessTokenResponse.Status.Code == 100
               && (DateTime.UtcNow - startTime) < timeout);


        if (accessTokenResponse.Status.Code != 200)
        {
            string msg = $"Uwierzytelnienie nie powiodło się. Kod statusu: {accessTokenResponse?.Status.Code}, opis: {accessTokenResponse?.Status.Description}.";

            throw new Exception(msg);
        }

        AuthOperationStatusResponse authResult = await ksefClient.GetAccessTokenAsync(authOperationInfo.AuthenticationToken.Token);
        return authResult;
    }
}