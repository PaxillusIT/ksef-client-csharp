using KSeF.Client.Api.Builders.PersonPermissions;
using KSeF.Client.Api.Builders.SubUnitPermissions;
using KSeF.Client.Core.Models.Authorization;
using KSeF.Client.Core.Models.Permissions;
using KSeF.Client.Core.Models.Permissions.Person;
using KSeF.Client.Core.Models.Permissions.SubUnit;
using KSeF.Client.Tests.Utils;
using System.Security.Cryptography.X509Certificates;

namespace KSeF.Client.Tests.Core.E2E.Permissions.SubunitPermission;

/// <summary>
/// Testy end-to-end dla uprawnień jednostek podrzędnych w systemie KSeF.
/// Obejmuje scenariusze nadawania i odwoływania uprawnień oraz ich weryfikację.
/// </summary>

[Collection("SubunitPermissionsScenarioE2ECollection")]
public class SubunitPermissionsE2ETests : TestBase
{
    private readonly SubunitPermissionsScenarioE2EFixture _fixture;

    private const int OperationSuccessfulStatusCode = 200;
    private string _unitAccessToken = string.Empty;
    private string _subunitAccessToken = string.Empty;

    public SubunitPermissionsE2ETests()
    {
        _fixture = new SubunitPermissionsScenarioE2EFixture();

        _fixture.UnitNipInternal = _fixture.Unit.Value + "-00001";
    }

    /// <summary>
    /// Test end-to-end dla pełnego cyklu zarządzania uprawnieniami jednostki podrzędnej:
    /// 1. Inicjalizacja i uwierzytelnienie jednostki głównej
    /// 2. Nadanie uprawnień do zarządzania jednostką podrzędną
    /// 3. Uwierzytelnienie w kontekście jednostki podrzędnej
    /// 4. Nadanie uprawnień administratora podmiotu podrzędnego
    /// 5. Weryfikacja nadanych uprawnień
    /// 6. Odwołanie uprawnień i weryfikacja
    /// </summary>
    [Fact]
    public async Task SubUnitPermission_E2E_GrantAndRevoke()
    {
        #region Inicjalizuje uwierzytelnienie jednostki głównej.
        // Arrange
        await AuthenticateAsUnitAsync();

        #endregion

        #region Nadanie uprawnienia SubunitManage, CredentialsManage do zarządzania jednostką podrzędną
        // Act
        PermissionsOperationStatusResponse grantOperationStatus = await GrantPersonPermissionsAsync();

        // Assert
        Assert.NotNull(grantOperationStatus);
        Assert.Equal(OperationSuccessfulStatusCode, grantOperationStatus.Status.Code);
        #endregion

        await Task.Delay(SleepTime);

        #region Uwierzytelnia w kontekście jednostki głównej jako jednostka podrzędna przy użyciu certyfikatu osobistego.
        // Act
        await AuthenticateAsSubunitAsync();

        #endregion

        await Task.Delay(SleepTime);

        #region Nadanie uprawnień administratora podmiotu podrzędnego jako jednostka podrzędna
        // Act
        OperationResponse grantSubunitResponse = await GrantSubunitPermissionsAsync();
        _fixture.GrantResponse = grantSubunitResponse;

        // Assert
        Assert.NotNull(_fixture.GrantResponse);
        Assert.NotNull(_fixture.GrantResponse.OperationReferenceNumber);

        await Task.Delay(SleepTime);

        PermissionsOperationStatusResponse grantSubunitStatus = await KsefClient.OperationsStatusAsync(_fixture.GrantResponse.OperationReferenceNumber, _subunitAccessToken);
        Assert.Equal(200, grantSubunitStatus.Status.Code);

        #endregion

        await Task.Delay(SleepTime);

        #region Wyszukaj uprawnienia nadane administratorowi jednostki podrzędnej
        // Act
        PagedPermissionsResponse<Client.Core.Models.Permissions.SubunitPermission> pagedPermissions = await SearchSubUnitAsync();
        _fixture.SearchResponse = pagedPermissions;

        // Assert
        Assert.NotNull(_fixture.SearchResponse);
        Assert.NotEmpty(_fixture.SearchResponse.Permissions);
        #endregion

        await Task.Delay(SleepTime);

        #region Cofnij uprawnienia nadane administratorowi jednostki podrzędnej
        // Act
        await RevokeSubUnitPermissionsAsync();

        // Assert
        Assert.NotNull(_fixture.RevokeStatusResults);
        Assert.NotEmpty(_fixture.RevokeStatusResults);
        Assert.Equal(_fixture.RevokeStatusResults.Count, _fixture.SearchResponse.Permissions.Count);
        Assert.All(_fixture.RevokeStatusResults, r =>
            Assert.True(r.Status.Code == OperationSuccessfulStatusCode,
                $"Operacja cofnięcia uprawnień nie powiodła się: {r.Status.Description}, szczegóły: [{string.Join(",", r.Status.Details ?? Array.Empty<string>())}]")
        );
        #endregion

        await Task.Delay(SleepTime);

        #region Sprawdź czy uprawnienia administratora jednostki podrzędnej zostały cofnięte
        // Act
        PagedPermissionsResponse<Client.Core.Models.Permissions.SubunitPermission> pagedPermissionsAfterRevoke = await SearchSubUnitAsync();
        _fixture.SearchResponse = pagedPermissionsAfterRevoke;

        // Assert
        Assert.Empty(pagedPermissionsAfterRevoke.Permissions);
        #endregion
    }

    /// <summary>
    /// Inicjalizuje uwierzytelnienie jednostki głównej.
    /// </summary>
    private async Task AuthenticateAsUnitAsync()
    {
        AuthOperationStatusResponse authInfo = await AuthenticationUtils.AuthenticateAsync(
            KsefClient,
            SignatureService,
            _fixture.Unit.Value);

        _unitAccessToken = authInfo.AccessToken.Token;
        Assert.NotEmpty(_unitAccessToken);
    }

    /// <summary>
    /// Uwierzytelnia w kontekście jednostki głównej jako jednostka podrzędna przy użyciu certyfikatu osobistego.
    /// </summary>
    private async Task AuthenticateAsSubunitAsync()
    {
        X509Certificate2 personalCertificate = CertificateUtils.GetPersonalCertificate(
            givenName: "Jan",
            surname: "Testowy",
            serialNumberPrefix: "TINPL",
            serialNumber: _fixture.Subunit.Value,
            commonName: "Jan Testowy Certificate");

        AuthOperationStatusResponse ownerAuthInfo = await AuthenticationUtils.AuthenticateAsync(
            KsefClient,
            SignatureService,
            _fixture.Unit.Value,
            Client.Core.Models.Authorization.ContextIdentifierType.Nip,
            personalCertificate);

        _subunitAccessToken = ownerAuthInfo.AccessToken.Token;
        Assert.NotEmpty(_subunitAccessToken);
    }

    /// <summary>
    /// Nadaje uprawnienia osobowe do zarządzania jednostką podrzędną (SubunitManage, CredentialsManage).
    /// </summary>
    /// <returns>Status operacji nadania uprawnień osobowych</returns>
    private async Task<PermissionsOperationStatusResponse> GrantPersonPermissionsAsync()
    {
        GrantPermissionsPersonRequest personGrantRequest = GrantPersonPermissionsRequestBuilder.Create()
            .WithSubject(new Client.Core.Models.Permissions.Person.SubjectIdentifier
            {
                Type = Client.Core.Models.Permissions.Person.SubjectIdentifierType.Nip,
                Value = _fixture.Subunit.Value
            })
            .WithPermissions(StandardPermissionType.SubunitManage, StandardPermissionType.CredentialsManage)
            .WithDescription("E2E test - nadanie uprawnień osobowych do zarządzania jednostką podrzędną")
            .Build();

        OperationResponse operationResponse = await KsefClient.GrantsPermissionPersonAsync(personGrantRequest, _unitAccessToken);

        Assert.NotNull(operationResponse.OperationReferenceNumber);

        await Task.Delay(SleepTime);

        return await KsefClient.OperationsStatusAsync(operationResponse.OperationReferenceNumber, _unitAccessToken);
    }

    /// <summary>
    /// Nadaje uprawnienia jednostce podrzędnej w kontekście jednostki głównej.
    /// </summary>
    /// <returns>Numer referencyjny operacji.</returns>
    private async Task<OperationResponse> GrantSubunitPermissionsAsync()
    {
        GrantPermissionsSubUnitRequest subunitGrantRequest =
            GrantSubUnitPermissionsRequestBuilder
            .Create()
            .WithSubject(_fixture.SubjectIdentifier)
            .WithContext(new Client.Core.Models.Permissions.SubUnit.ContextIdentifier
            {
                Type = Client.Core.Models.Permissions.SubUnit.ContextIdentifierType.InternalId,
                Value = _fixture.UnitNipInternal
            })
            .WithDescription("E2E test grant sub-unit")
            .Build();

        OperationResponse operationResponse = await KsefClient
            .GrantsPermissionSubUnitAsync(subunitGrantRequest, _subunitAccessToken, CancellationToken);

        return operationResponse;
    }

    /// <summary>
    /// Wyszukuje uprawnienia nadane jednostce podrzędnej.
    /// </summary>
    /// <returns>Stronicowana lista uprawnień nadanych jednostce podrzędnej.</returns>
    private async Task<PagedPermissionsResponse<Client.Core.Models.Permissions.SubunitPermission>> SearchSubUnitAsync()
    {
        PagedPermissionsResponse<Client.Core.Models.Permissions.SubunitPermission> pagedSubunitPermissions =
            await SearchSubUnitAdminPermissionsAsync();

        return pagedSubunitPermissions;
    }

    /// <summary>
    /// Wyszukuje uprawnienia administratorskie nadane jednostce podrzędnej.
    /// </summary>
    /// <returns>Stronicowana lista uprawnień administratorskich nadanych jednostce podrzędnej.</returns>
    private async Task<PagedPermissionsResponse<Client.Core.Models.Permissions.SubunitPermission>> SearchSubUnitAdminPermissionsAsync()
    {
        SubunitPermissionsQueryRequest subunitPermissionsQueryRequest = new SubunitPermissionsQueryRequest();
        PagedPermissionsResponse<Client.Core.Models.Permissions.SubunitPermission> response =
            await KsefClient
            .SearchSubunitAdminPermissionsAsync(
                subunitPermissionsQueryRequest,
                _subunitAccessToken,
                pageOffset: 0,
                pageSize: 10,
                CancellationToken);

        return response;
    }

    /// <summary>
    /// Odwołuje uprawnienia nadane jednostce podrzędnej.
    /// </summary>
    /// <returns></returns>
    private async Task RevokeSubUnitPermissionsAsync()
    {
        List<OperationResponse> revokeResponses = new List<Client.Core.Models.Permissions.OperationResponse>();
        // Uruchomienie operacji cofania
        foreach (Client.Core.Models.Permissions.SubunitPermission permission in _fixture.SearchResponse.Permissions)
        {
            Client.Core.Models.Permissions.OperationResponse response = await KsefClient.RevokeCommonPermissionAsync(permission.Id, _subunitAccessToken, CancellationToken.None);
            revokeResponses.Add(response);
        }

        // Sprawdzenie statusów wszystkich operacji
        foreach (Client.Core.Models.Permissions.OperationResponse revokeResponse in revokeResponses)
        {
            await Task.Delay(SleepTime);
            Client.Core.Models.Permissions.PermissionsOperationStatusResponse revokeStatus = await KsefClient.OperationsStatusAsync(revokeResponse.OperationReferenceNumber, _subunitAccessToken);
            _fixture.RevokeStatusResults.Add(revokeStatus);
        }
    }
}