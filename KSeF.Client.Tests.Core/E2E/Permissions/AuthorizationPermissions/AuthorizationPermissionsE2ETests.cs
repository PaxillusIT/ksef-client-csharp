using KSeF.Client.Api.Builders.AuthorizationPermissions;
using KSeF.Client.Core.Models.Authorization;
using KSeF.Client.Core.Models.Permissions;
using KSeF.Client.Core.Models.Permissions.AuthorizationEntity;
using KSeF.Client.Core.Models.Permissions.Entity;
using KSeF.Client.Tests.Utils;
using StandardPermissionType = KSeF.Client.Core.Models.Permissions.AuthorizationEntity.StandardPermissionType;

namespace KSeF.Client.Tests.Core.E2E.Permissions.AuthorizationPermission;

[Collection("AuthorizationPermissionsScenarioE2ECollection")]
public class AuthorizationPermissionsE2ETests : TestBase
{
    private readonly AuthorizationPermissionsScenarioE2EFixture Fixture;
    
    private const int OperationSuccessfulStatusCode = 200;
    private string accessToken = string.Empty;

    public AuthorizationPermissionsE2ETests()
    {
        Fixture = new AuthorizationPermissionsScenarioE2EFixture();
        AuthOperationStatusResponse authOperationStatusResponse =
            AuthenticationUtils.AuthenticateAsync(KsefClient, SignatureService).GetAwaiter().GetResult();
        accessToken = authOperationStatusResponse.AccessToken.Token;
        Fixture.SubjectIdentifier.Value = MiscellaneousUtils.GetRandomNip();
    }

    /// <summary>
    /// Nadaje uprawnienia, wyszukuje czy zostały nadane, odwołuje uprawnienia i sprawdza, czy po odwołaniu uprawnienia już nie występują.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task AuthorizationPermissions_E2E_GrantSearchRevokeSearch()
    {
        #region Nadaj uprawnienia
        // Act
        OperationResponse operationResponse = await GrantPermissionsAsync();
        Fixture.GrantResponse = operationResponse;

        // Assert
        Assert.NotNull(Fixture.GrantResponse);
        Assert.True(!string.IsNullOrEmpty(Fixture.GrantResponse.OperationReferenceNumber));
        #endregion

        await Task.Delay(SleepTime);

        #region Wyszukaj — powinny się pojawić
        // Act
        PagedAuthorizationsResponse<AuthorizationGrant> entityRolesPaged = await SearchGrantedRolesAsync();

        // Assert
        Assert.NotNull(entityRolesPaged);
        Assert.NotEmpty(entityRolesPaged.AuthorizationGrants);
        Fixture.SearchResponse = entityRolesPaged;

        #endregion

        await Task.Delay(SleepTime);

        #region Cofnij uprawnienia
        // Act
        await RevokePermissionsAsync();
        Assert.NotNull(Fixture.RevokeStatusResults);
        Assert.NotEmpty(Fixture.RevokeStatusResults);
        Assert.Equal(Fixture.RevokeStatusResults.Count, Fixture.SearchResponse.AuthorizationGrants.Count);
        Assert.All(Fixture.RevokeStatusResults, r =>
            Assert.True(r.Status.Code == OperationSuccessfulStatusCode,
                $"Operacja cofnięcia uprawnień nie powiodła się: {r.Status.Description}, szczegóły: [{string.Join(",", r.Status.Details ?? Array.Empty<string>())}]")
        );

        #endregion

        await Task.Delay(SleepTime);

        #region Wyszukaj ponownie — nie powinno być wpisów
        PagedAuthorizationsResponse<AuthorizationGrant> entityRolesAfterRevoke =
            await SearchGrantedRolesAsync();
        Fixture.SearchResponse = entityRolesAfterRevoke;

        // Assert
        Assert.NotNull(Fixture.SearchResponse);
        Assert.Empty(Fixture.SearchResponse.AuthorizationGrants);
        #endregion
    }

    /// <summary>
    /// Nadaje uprawnienia.
    /// </summary>
    /// <returns>Numer referencyjny operacji</returns>
    private async Task<OperationResponse> GrantPermissionsAsync()
    {
        GrantAuthorizationPermissionsRequest grantPermissionAuthorizationRequest =
            GrantAuthorizationPermissionsRequestBuilder
            .Create()
            .WithSubject(Fixture.SubjectIdentifier)
            .WithPermission(StandardPermissionType.SelfInvoicing)
            .WithDescription("E2E test grant")
            .Build();

        OperationResponse operationResponse = await KsefClient
            .GrantsAuthorizationPermissionAsync(grantPermissionAuthorizationRequest,
            accessToken, CancellationToken);

        return operationResponse;
    }

    /// <summary>
    /// Wyszukuje uprawnienia.
    /// </summary>
    /// <returns>Stronicowana lista nadanych uprawnień.</returns>
    private async Task<PagedAuthorizationsResponse<AuthorizationGrant>> SearchGrantedRolesAsync()
    {
        EntityAuthorizationsQueryRequest request = new EntityAuthorizationsQueryRequest();
        PagedAuthorizationsResponse<AuthorizationGrant> entityRolesPaged = await KsefClient
            .SearchEntityAuthorizationGrantsAsync(
                request,
                accessToken,
                pageOffset: 0,
                pageSize: 10,
                CancellationToken
            );

        return entityRolesPaged;
    }

    /// <summary>
    /// Odwołuje uprawnienia.
    /// </summary>
    private async Task RevokePermissionsAsync()
    {
        List<OperationResponse> revokeResponses = new List<Client.Core.Models.Permissions.OperationResponse>();

        // Uruchomienie operacji cofania
        foreach (Client.Core.Models.Permissions.AuthorizationGrant permission in Fixture.SearchResponse.AuthorizationGrants)
        {
            Client.Core.Models.Permissions.OperationResponse resp = await KsefClient.RevokeAuthorizationsPermissionAsync(permission.Id, accessToken, CancellationToken.None);
            revokeResponses.Add(resp);
        }

        // Sprawdzenie statusów wszystkich operacji
        foreach (Client.Core.Models.Permissions.OperationResponse revokeResponse in revokeResponses)
        {
            await Task.Delay(SleepTime);
            Client.Core.Models.Permissions.PermissionsOperationStatusResponse status = await KsefClient.OperationsStatusAsync(revokeResponse.OperationReferenceNumber, accessToken);
            Fixture.RevokeStatusResults.Add(status);
        }
    }
}
