using KSeF.Client.Core.Interfaces;
using KSeF.Client.Core.Models.Authorization;
using KSeF.Client.Api.Builders.Auth;
using System.Text;
using KSeF.Client.Core.Models;

namespace KSeF.Client.Api.Services;

/// <inheritdoc />
public class AuthCoordinator : IAuthCoordinator
{
    private readonly IKSeFClient _ksefClient;

    public AuthCoordinator(
        IKSeFClient ksefClient
        )
    {
        _ksefClient = ksefClient;
    }

    /// <inheritdoc />
    public async Task<AuthOperationStatusResponse> AuthKsefTokenAsync(
        ContextIdentifierType contextIdentifierType,
        string contextIdentifierValue,
        string tokenKsef,
        ICryptographyService cryptographyService,
        EncryptionMethodEnum encryptionMethod = EncryptionMethodEnum.ECDsa,
        AuthorizationPolicy ipAddressPolicy = default,
        CancellationToken cancellationToken = default)
    {
        // 1) Pobranie challenge i timestamp
        AuthChallengeResponse challengeResponse = await _ksefClient
            .GetAuthChallengeAsync(cancellationToken);

        string challenge = challengeResponse.Challenge;
        DateTimeOffset timestamp = challengeResponse.Timestamp;

        long timestampMs = challengeResponse.Timestamp.ToUnixTimeMilliseconds();

        // 2) Tworzenie ciągu token|timestamp
        string tokenWithTimestamp = $"{tokenKsef}|{timestampMs}";
        byte[] tokenBytes = Encoding.UTF8.GetBytes(tokenWithTimestamp);

        // 3) Szyfrowanie RSA-OAEP SHA-256
        byte[] tokenEncryptedBytes = encryptionMethod switch
        {
            EncryptionMethodEnum.Rsa => cryptographyService.EncryptKsefTokenWithRSAUsingPublicKey(tokenBytes),
            EncryptionMethodEnum.ECDsa => cryptographyService.EncryptWithECDSAUsingPublicKey(tokenBytes),
            _ => throw new ArgumentOutOfRangeException(nameof(encryptionMethod))
        };

        string encryptedToken = Convert.ToBase64String(tokenEncryptedBytes);

        // 4) Budowa żądania
        IAuthKsefTokenRequestBuilderWithEncryptedToken requestBuilder = AuthKsefTokenRequestBuilder
            .Create()
            .WithChallenge(challenge)
            .WithContext(contextIdentifierType, contextIdentifierValue)
            .WithEncryptedToken(encryptedToken);

        if (ipAddressPolicy != null)
            requestBuilder = requestBuilder.WithAuthorizationPolicy(ipAddressPolicy);

        AuthKsefTokenRequest authKsefTokenRequest = requestBuilder.Build();

        // 5) Wysłanie do KSeF
        SignatureResponse submissionResponse = await _ksefClient
            .SubmitKsefTokenAuthRequestAsync(authKsefTokenRequest, cancellationToken);

        // 6) Odpytanie o gotowość tokenu
        AuthStatus authStatus;
        DateTime startTime = DateTime.UtcNow;
        TimeSpan timeout = TimeSpan.FromMinutes(2);

        do
        {
            authStatus = await _ksefClient.GetAuthStatusAsync(submissionResponse.ReferenceNumber, submissionResponse.AuthenticationToken.Token, cancellationToken);

            Console.WriteLine(
                $"Polling: StatusCode={authStatus.Status.Code}, " +
                $"Description='{authStatus.Status.Description}', " +                
                $"Details='{string.Join(", ",(authStatus.Status.Details ?? new List<string>()))}', " +                
                $"Elapsed={DateTime.UtcNow - startTime:mm\\:ss}");
            
            if (authStatus.Status.Code == 400)
            {
                var exMsg = $"Polling: StatusCode={authStatus.Status.Code}, Description={authStatus.Status.Description}, Details={string.Join(", ", (authStatus.Status.Details ?? new List<string>()))}'";
                throw new Exception(exMsg);
            }

            if (authStatus.Status.Code != 200 && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
        while (authStatus.Status.Code == 100
            && !cancellationToken.IsCancellationRequested
            && (DateTime.UtcNow - startTime) < timeout);

        if (authStatus.Status.Code != 200)
        {
            Console.WriteLine("Timeout: Brak tokena po 2 minutach.");
            var exMsg = $"Polling: StatusCode={authStatus.Status.Code}, Description={authStatus.Status.Description}, Details={string.Join(", ", (authStatus.Status.Details ?? new List<string>()))}'";
            throw new Exception("Timeout Uwierzytelniania: Brak tokena po 2 minutach." + exMsg);
        }
        var accessTokenResponse = await _ksefClient.GetAccessTokenAsync(submissionResponse.AuthenticationToken.Token, cancellationToken);

        // 7) Zwróć token            
        return accessTokenResponse;
    }


    /// <inheritdoc />
    public async Task<AuthOperationStatusResponse> AuthAsync(
        ContextIdentifierType contextIdentifierType,
        string contextIdentifierValue,
        SubjectIdentifierTypeEnum identifierType,
        Func<string, Task<string>> xmlSigner,
        AuthorizationPolicy ipAddressPolicy = default,
        CancellationToken cancellationToken = default,
        bool verifyCertificateChain = false)
    {
        // 1) Challenge
        AuthChallengeResponse challengeResponse = await _ksefClient
            .GetAuthChallengeAsync(cancellationToken);

        string challenge = challengeResponse.Challenge;

        // 2) Budowa obiektu AuthKsefTokenRequest
        IAuthTokenRequestBuilderReady authTokenRequest =
            AuthTokenRequestBuilder
            .Create()
            .WithChallenge(challenge)
            .WithContext(contextIdentifierType, contextIdentifierValue)
            .WithIdentifierType(identifierType);

        if (ipAddressPolicy != null)
        {
            authTokenRequest = authTokenRequest
            .WithAuthorizationPolicy(ipAddressPolicy);               
        }

        AuthTokenRequest authorizeRequest = authTokenRequest.Build();

        // 3) Serializacja do XML
        string unsignedXml = AuthTokenRequestSerializer.SerializeToXmlString(authorizeRequest);

        // 4) wywołanie mechanizmu podpisującego XML
        string signedXml = await xmlSigner.Invoke(unsignedXml);

        // 5)// Przesłanie podpisanego XML do systemu KSeF
        SignatureResponse authSubmission = await _ksefClient
            .SubmitXadesAuthRequestAsync(signedXml, false, cancellationToken);

        // 6) Odpytanie o gotowość tokenu
        AuthStatus authStatus;
        DateTime startTime = DateTime.UtcNow;
        TimeSpan timeout = TimeSpan.FromMinutes(2);

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

    /// <inheritdoc />
    public Task<TokenInfo> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        => _ksefClient.RefreshAccessTokenAsync(refreshToken, cancellationToken)
                         .ContinueWith(t => t.Result.AccessToken, cancellationToken);
}
