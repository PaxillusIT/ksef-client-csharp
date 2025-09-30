using KSeF.Client.Core.Models;
using KSeF.Client.Core.Models.Authorization;
using KSeF.Client.Tests.Utils;

namespace KSeF.Client.Tests.Core.E2E.KsefToken;

[Collection("KsefTokenScenario")]
public class KsefTokenE2ETests : TestBase
{
    private const int SuccessfulAuthStatusCode = 200;
    private const int PendingAuthStatusCode = 100;
    private const int TokenActivationTimeoutSeconds = 60;

    private string AccessToken { get; }
    private string Nip { get; }

    public KsefTokenE2ETests()
    {
        Nip = MiscellaneousUtils.GetRandomNip();
        AuthOperationStatusResponse authInfo = AuthenticationUtils.AuthenticateAsync(KsefClient, SignatureService, Nip)
                                          .GetAwaiter()
                                          .GetResult();
        AccessToken = authInfo.AccessToken.Token;
    }

    /// <summary>
    /// Test E2E weryfikujący pełny cykl życia tokena KSeF:
    /// generowanie, oczekiwanie na aktywację, uwierzytelnienie tokenem, unieważnienie oraz weryfikacja unieważnienia.
    /// </summary>
    /// <remarks>
    /// Kroki:
    /// 1) Generuje token KSeF z uprawnieniami (InvoiceRead, InvoiceWrite).
    /// 2) Czeka aż status tokena zmieni się na Active.
    /// 3) Uwierzytelnia się do KSeF używając tokena (RSA-OAEP SHA-256 na ciągu "token|timestamp") i pobiera access/refresh token.
    /// 4) Unieważnia wygenerowany token.
    /// 5) Sprawdza, że token ma status Revoked.
    /// Między krokami stosowane są krótkie opóźnienia (SleepTime), aby umożliwić przetwarzanie po stronie KSeF.
    /// </remarks>
    [Fact]
    public async Task KsefTokensAsync_FullIntegrationFlow_AllStepsSucceed()
    {
        // 1) Wygeneruj token KSeF z uprawnieniami
        KsefTokenResponse tokenResponse = await GenerateKsefTokenAsync("E2E token");
        Assert.NotNull(tokenResponse);
        Assert.False(string.IsNullOrWhiteSpace(tokenResponse.ReferenceNumber));
        Assert.False(string.IsNullOrWhiteSpace(tokenResponse.Token));
        await Task.Delay(SleepTime);

        // 2) Poczekaj, aż token stanie się aktywny
        AuthenticationKsefToken activeToken = await WaitUntilTokenStatusAsync(tokenResponse.ReferenceNumber, AuthenticationKsefTokenStatus.Active, TimeSpan.FromSeconds(TokenActivationTimeoutSeconds));
        Assert.NotNull(activeToken);
        Assert.Equal(AuthenticationKsefTokenStatus.Active, activeToken.Status);
        await Task.Delay(SleepTime);

        // 3) Uwierzytelnij w KSeF przy użyciu tokena KSeF
        AuthOperationStatusResponse authResult = await AuthenticateWithKsefTokenAsync(tokenResponse.Token, Nip);
        Assert.NotNull(authResult);
        Assert.False(string.IsNullOrWhiteSpace(authResult.AccessToken?.Token));
        Assert.False(string.IsNullOrWhiteSpace(authResult.RefreshToken?.Token));
        await Task.Delay(SleepTime);

        // 4) Unieważnij token
        await RevokeKsefTokenAsync(tokenResponse.ReferenceNumber);
        await Task.Delay(SleepTime);

        // 5) Zweryfikuj, że token został unieważniony
        AuthenticationKsefToken revokedToken = await GetKsefTokenByReferenceAsync(tokenResponse.ReferenceNumber);
        Assert.NotNull(revokedToken);
        Assert.Equal(AuthenticationKsefTokenStatus.Revoked, revokedToken.Status);
    }

    /// <summary>
    /// Generuje token KSeF z podanym opisem oraz zestawem uprawnień (odczyt i zapis faktur).
    /// </summary>
    /// <param name="description">Opis tokena widoczny w KSeF.</param>
    /// <returns><see cref="KsefTokenResponse"/> zawierająca numer referencyjny oraz wygenerowany token.</returns>
    private async Task<KsefTokenResponse> GenerateKsefTokenAsync(string description)
    {
        KsefTokenRequest request = new KsefTokenRequest
        {
            Permissions =
            [
                KsefTokenPermissionType.InvoiceRead,
                KsefTokenPermissionType.InvoiceWrite
            ],
            Description = description
        };

        return await KsefClient.GenerateKsefTokenAsync(request, AccessToken);
    }

    /// <summary>
    /// Cyklicznie sprawdza status tokena do momentu osiągnięcia oczekiwanego statusu lub upłynięcia limitu czasu.
    /// </summary>
    /// <param name="referenceNumber">Numer referencyjny tokena.</param>
    /// <param name="desiredStatus">Docelowy status tokena, na który oczekujemy.</param>
    /// <param name="timeout">Maksymalny czas oczekiwania.</param>
    /// <returns>Ostatnio odczytany <see cref="AuthenticationKsefToken"/>. Jeśli przekroczono limit czasu, może nie mieć oczekiwanego statusu.</returns>
    private async Task<AuthenticationKsefToken> WaitUntilTokenStatusAsync(string referenceNumber, AuthenticationKsefTokenStatus desiredStatus, TimeSpan timeout)
    {
        DateTime start = DateTime.UtcNow;
        AuthenticationKsefToken current;
        do
        {
            current = await KsefClient.GetKsefTokenAsync(referenceNumber, AccessToken);
            if (current.Status == desiredStatus)
                return current;

            await Task.Delay(SleepTime);
        } while (DateTime.UtcNow - start < timeout);

        return current;
    }

    /// <summary>
    /// Przeprowadza pełny proces uwierzytelnienia przy użyciu tokena KSeF.
    /// </summary>
    /// <remarks>
    /// Pobiera wyzwanie (challenge) wraz ze znacznikiem czasu, szyfruje ciąg "token|timestampMs" kluczem publicznym KSeF (RSA-OAEP SHA-256),
    /// wysyła żądanie uwierzytelnienia, sonduje status (100 -> 200) i pobiera access/refresh token.
    /// </remarks>
    /// <param name="ksefToken">Wygenerowany token KSeF w postaci jawnej (string).</param>
    /// <param name="nip">Identyfikator kontekstu (NIP) dla uwierzytelnienia.</param>
    /// <returns><see cref="AuthOperationStatusResponse"/> zawierająca access oraz refresh token.</returns>
    private async Task<AuthOperationStatusResponse> AuthenticateWithKsefTokenAsync(string ksefToken, string nip)
    {
        // 1) Pobierz challenge i timestamp
        AuthChallengeResponse challenge = await KsefClient.GetAuthChallengeAsync();
        long timestampMs = challenge.Timestamp.ToUnixTimeMilliseconds();

        // 2) Przygotuj "token|timestamp" i zaszyfruj RSA-OAEP SHA-256 zgodnie z wymaganiem API
        string tokenWithTimestamp = $"{ksefToken}|{timestampMs}";
        byte[] tokenBytes = System.Text.Encoding.UTF8.GetBytes(tokenWithTimestamp);
        byte[] encrypted = CryptographyService.EncryptKsefTokenWithRSAUsingPublicKey(tokenBytes);
        string encryptedTokenB64 = Convert.ToBase64String(encrypted);

        // 3) Wyślij żądanie uwierzytelnienia tokenem KSeF
        AuthKsefTokenRequest request = new AuthKsefTokenRequest
        {
            Challenge = challenge.Challenge,
            ContextIdentifier = new AuthContextIdentifier
            {
                Type = ContextIdentifierType.Nip,
                Value = nip
            },
            EncryptedToken = encryptedTokenB64,
            AuthorizationPolicy = null
        };

        SignatureResponse signature = await KsefClient.SubmitKsefTokenAuthRequestAsync(request, CancellationToken.None);
        Assert.False(string.IsNullOrWhiteSpace(signature.ReferenceNumber));
        Assert.False(string.IsNullOrWhiteSpace(signature.AuthenticationToken?.Token));

        // 4) Pollowanie statusu aż do 200 lub timeoutu
        AuthStatus status;
        DateTime startTime = DateTime.UtcNow;
        TimeSpan pollTimeout = TimeSpan.FromMinutes(2);

        do
        {
            status = await KsefClient.GetAuthStatusAsync(signature.ReferenceNumber, signature.AuthenticationToken.Token);
            if (status.Status.Code != SuccessfulAuthStatusCode)
            {
                await Task.Delay(SleepTime);
            }
        }
        while (status.Status.Code == PendingAuthStatusCode && (DateTime.UtcNow - startTime) < pollTimeout);

        Assert.Equal(SuccessfulAuthStatusCode, status.Status.Code);

        // 5) Pobierz access/refresh tokeny
        AuthOperationStatusResponse tokens = await KsefClient.GetAccessTokenAsync(signature.AuthenticationToken.Token);
        return tokens;
    }

    /// <summary>
    /// Pobiera informacje o tokenie KSeF na podstawie numeru referencyjnego.
    /// </summary>
    /// <param name="referenceNumber">Numer referencyjny tokena.</param>
    /// <returns>Obiekt <see cref="AuthenticationKsefToken"/> z aktualnym statusem i metadanymi.</returns>
    private async Task<AuthenticationKsefToken> GetKsefTokenByReferenceAsync(string referenceNumber)
        => await KsefClient.GetKsefTokenAsync(referenceNumber, AccessToken);

    /// <summary>
    /// Unieważnia token KSeF o podanym numerze referencyjnym.
    /// </summary>
    /// <param name="referenceNumber">Numer referencyjny tokena do unieważnienia.</param>
    private async Task RevokeKsefTokenAsync(string referenceNumber)
        => await KsefClient.RevokeKsefTokenAsync(referenceNumber, AccessToken, CancellationToken.None);
}