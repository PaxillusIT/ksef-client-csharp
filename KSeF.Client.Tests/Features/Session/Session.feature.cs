using KSeF.Client.Core.Exceptions;
using KSeF.Client.Core.Models.Invoices;
using KSeF.Client.Tests.Utils;

namespace KSeF.Client.Tests.Features;

[CollectionDefinition("Session.feature")]
[Trait("Category", "Features")]
[Trait("Features", "sessions.feature")]
public class InteractiveSessionTests : KsefIntegrationTestBase
{
    private const string OwnerContextNip = "6351111111";
    private const string SecondContextNip = "6451111144";
    private const string operationForbidden = "HTTP 403: Forbidden";


    [Theory]
    [InlineData(SystemCodeEnum.FA2)]
    [InlineData(SystemCodeEnum.FA3)]
    [Trait("Scenario", "Pytam o status aktualnej sesji interaktywnej")]
    public async Task GivenActiveInteractiveSession_WhenCheckingStatus_ThenReturnsValidStatus(SystemCodeEnum systemCode)
    {
        var authResult = await AuthenticationUtils.AuthenticateAsync(KsefClient, SignatureService, OwnerContextNip);

        var encryptionData = CryptographyService.GetEncryptionData();

        var openOnlineSessionRequest = OpenOnlineSessionRequestBuilder
           .Create()
           .WithFormCode(systemCode: SystemCodeHelper.GetSystemCode(systemCode), schemaVersion: "1-0E", value: "FA")
           .WithEncryption(
               encryptedSymmetricKey: encryptionData.EncryptionInfo.EncryptedSymmetricKey,
               initializationVector: encryptionData.EncryptionInfo.InitializationVector)
           .Build();

        var openOnlineSessionResponse = await KsefClient.OpenOnlineSessionAsync(openOnlineSessionRequest, authResult.AccessToken.Token);
        Assert.NotNull(openOnlineSessionResponse);
        Assert.NotNull(openOnlineSessionResponse.ReferenceNumber);

        var sessionStatusResponse = await KsefClient.GetSessionStatusAsync(openOnlineSessionResponse.ReferenceNumber, authResult.AccessToken.Token);

        Assert.NotNull(sessionStatusResponse);
        Assert.NotNull(sessionStatusResponse.Status);
        Assert.True(sessionStatusResponse.Status.Code == 100);
    }

    [Theory]
    [InlineData(SystemCodeEnum.FA2)]
    [InlineData(SystemCodeEnum.FA3)]
    [Trait("Scenario", "Pytam o status innej sesji interaktywnej z mojego kontekstu")]
    public async Task GivenSessionFromSameContext_WhenCheckingStatus_ThenReturnsValidStatus(SystemCodeEnum systemCode)
    {
        var authResult = await AuthenticationUtils.AuthenticateAsync(KsefClient, SignatureService, OwnerContextNip);

        var encryptionData = CryptographyService.GetEncryptionData();

        var openOnlineSessionRequest = OpenOnlineSessionRequestBuilder
               .Create()
               .WithFormCode(systemCode: SystemCodeHelper.GetSystemCode(systemCode), schemaVersion: "1-0E", value: "FA")
               .WithEncryption(
                   encryptedSymmetricKey: encryptionData.EncryptionInfo.EncryptedSymmetricKey,
                   initializationVector: encryptionData.EncryptionInfo.InitializationVector)
               .Build();

        var openOnlineSessionResponse = await KsefClient.OpenOnlineSessionAsync(openOnlineSessionRequest, authResult.AccessToken.Token);
        Assert.NotNull(openOnlineSessionResponse.ReferenceNumber);

        var sessionStatusResponse = await KsefClient.GetSessionStatusAsync(openOnlineSessionResponse.ReferenceNumber, authResult.AccessToken.Token);
        Assert.NotNull(sessionStatusResponse);
        Assert.True(sessionStatusResponse.Status.Code == 100);

        var openSecondOnlineSessionResponse = await KsefClient.OpenOnlineSessionAsync(openOnlineSessionRequest, authResult.AccessToken.Token);
        Assert.NotNull(openSecondOnlineSessionResponse.ReferenceNumber);

        var secondSessionStatusResponse = await KsefClient.GetSessionStatusAsync(openSecondOnlineSessionResponse.ReferenceNumber, authResult.AccessToken.Token);

        Assert.NotNull(secondSessionStatusResponse);
        Assert.True(secondSessionStatusResponse.Status.Code == 100);
    }

    [Theory]
    [InlineData(SystemCodeEnum.FA2)]
    [InlineData(SystemCodeEnum.FA3)]
    [Trait("Scenario", "Pytam o status innej sesji interaktywnej z innego kontekstu")]
    public async Task GivenSessionFromDifferentContext_WhenCheckingStatus_ThenReturnsAuthorizationError(SystemCodeEnum systemCode)
    {
        var authResult = await AuthenticationUtils.AuthenticateAsync(KsefClient, SignatureService, OwnerContextNip);

        var encryptionData = CryptographyService.GetEncryptionData();

        var openOnlineSessionRequest = OpenOnlineSessionRequestBuilder
               .Create()
               .WithFormCode(systemCode: SystemCodeHelper.GetSystemCode(systemCode), schemaVersion: "1-0E", value: "FA")
               .WithEncryption(
                   encryptedSymmetricKey: encryptionData.EncryptionInfo.EncryptedSymmetricKey,
                   initializationVector: encryptionData.EncryptionInfo.InitializationVector)
               .Build();

        var openOnlineSessionResponse = await KsefClient.OpenOnlineSessionAsync(openOnlineSessionRequest, authResult.AccessToken.Token);
        Assert.NotNull(openOnlineSessionResponse);
        Assert.NotNull(openOnlineSessionResponse.ReferenceNumber);

        var sessionStatusResponse = await KsefClient.GetSessionStatusAsync(openOnlineSessionResponse.ReferenceNumber, authResult.AccessToken.Token);
        Assert.True(sessionStatusResponse.Status.Code == 100);

        authResult = await AuthenticationUtils.AuthenticateAsync(KsefClient, SignatureService, SecondContextNip); //new context
        var openSecondOnlineSessionResponse = await KsefClient.OpenOnlineSessionAsync(openOnlineSessionRequest, authResult.AccessToken.Token);

        var secondSessionStatusResponse = await KsefClient.GetSessionStatusAsync(openSecondOnlineSessionResponse.ReferenceNumber, authResult.AccessToken.Token);
        Assert.NotNull(secondSessionStatusResponse);
        Assert.NotNull(secondSessionStatusResponse.Status);
        Assert.True(secondSessionStatusResponse.Status.Code == 100);

        var callFromSecondContextResponse = await Assert.ThrowsAsync<KsefApiException>(() =>
                    KsefClient.GetSessionStatusAsync(openOnlineSessionResponse.ReferenceNumber, authResult.AccessToken.Token));

        Assert.NotNull(callFromSecondContextResponse);
        Assert.Equal(operationForbidden, callFromSecondContextResponse?.Message);
    }

    [Theory]
    [InlineData(SystemCodeEnum.FA2)]
    [InlineData(SystemCodeEnum.FA3)]
    [Trait("Scenario", "Zamykam sesję interaktywną")]
    public async Task GivenInteractiveSession_WhenClosingSession_ThenSessionIsClosed(SystemCodeEnum systemCode)
    {
        var authResult = await AuthenticationUtils.AuthenticateAsync(KsefClient, SignatureService, OwnerContextNip);

        var encryptionData = CryptographyService.GetEncryptionData();

        var openOnlineSessionRequest = OpenOnlineSessionRequestBuilder
               .Create()
               .WithFormCode(systemCode: SystemCodeHelper.GetSystemCode(systemCode), schemaVersion: "1-0E", value: "FA")
               .WithEncryption(
                   encryptedSymmetricKey: encryptionData.EncryptionInfo.EncryptedSymmetricKey,
                   initializationVector: encryptionData.EncryptionInfo.InitializationVector)
               .Build();

        var openOnlineSessionResponse = await KsefClient.OpenOnlineSessionAsync(openOnlineSessionRequest, authResult.AccessToken.Token);
        var sessionStatusResponse = await KsefClient.GetSessionStatusAsync(openOnlineSessionResponse.ReferenceNumber, authResult.AccessToken.Token);
        Assert.NotNull(sessionStatusResponse);
        Assert.NotNull(sessionStatusResponse.Status);
        Assert.True(sessionStatusResponse.Status.Code == 100);

        await KsefClient.CloseOnlineSessionAsync(openOnlineSessionResponse.ReferenceNumber, authResult.AccessToken.Token);
        var closedSessionStatusResponse = await KsefClient.GetSessionStatusAsync(openOnlineSessionResponse.ReferenceNumber, authResult.AccessToken.Token);

        Assert.NotNull(closedSessionStatusResponse);
        Assert.NotNull(closedSessionStatusResponse.Status);
        Assert.False(closedSessionStatusResponse.Status.Code == 100);
        Assert.True(closedSessionStatusResponse.Status.Code == 440);
    }
}