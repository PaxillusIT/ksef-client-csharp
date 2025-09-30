using KSeF.Client.Api.Builders.IndirectEntityPermissions;
using KSeF.Client.Api.Builders.PersonPermissions;
using KSeF.Client.Core.Models.Permissions;
using KSeF.Client.Core.Models.Permissions.IndirectEntity;
using KSeF.Client.Core.Models.Permissions.Person;
using KSeF.Client.Tests.Utils;

namespace KSeF.Client.Tests.Core.E2E.Permissions.IndirectPermission;

/// <summary>
/// Testy end-to-end dla nadawania uprawnień w sposób pośredni systemie KSeF.
/// Obejmuje scenariusze nadawania i odwoływania uprawnień oraz ich weryfikację.
/// </summary>
[Collection("IndirectPermissionScenario")]
public class IndirectPermissionE2ETests : TestBase
{
    private const int OperationSuccessfulStatusCode = 200;

    private string ownerAccessToken { get; set; }
    private string ownerNip { get; set; }

    private string delegateAccessToken { get; set; }
    private string delegateNip { get; set; }

    private Client.Core.Models.Permissions.IndirectEntity.SubjectIdentifier Subject { get; } =
        new Client.Core.Models.Permissions.IndirectEntity.SubjectIdentifier
        {
            Type = Client.Core.Models.Permissions.IndirectEntity.SubjectIdentifierType.Nip
        };


    public IndirectPermissionE2ETests()
    {
        ownerNip = MiscellaneousUtils.GetRandomNip();
        delegateNip = MiscellaneousUtils.GetRandomNip();
        Subject.Value = MiscellaneousUtils.GetRandomNip();
    }

    /// <summary>
    /// Wykonuje kompletny scenariusz obsługi uprawnień pośrednich E2E:
    /// 1. Nadaje uprawnienia CredentialsManage dla pośrednika
    /// 2. Nadaje uprawnienia pośrednie
    /// 3. Wyszukuje nadane uprawnienia
    /// 4. Usuwa uprawnienia
    /// 5. Sprawdza, że uprawnienia zostały poprawnie usunięte
    /// </summary>
    [Fact]
    public async Task IndirectPermission_E2E_GrantSearchRevokeSearch()
    {
        // Arrange: Uwierzytelnienie właściciela 
        ownerAccessToken = (await AuthenticationUtils.AuthenticateAsync(KsefClient, SignatureService, ownerNip)).AccessToken.Token;

        // Act: 1) Nadanie uprawnień CredentialsManage dla pośrednika
        PermissionsOperationStatusResponse personGrantStatus = await GrantCredentialsManageToDelegateAsync();

        // Assert
        Assert.NotNull(personGrantStatus);
        Assert.Equal(OperationSuccessfulStatusCode, personGrantStatus.Status.Code);

        // Arrange: Uwierzytelnienie pośrednika (Arrange)
        delegateAccessToken = (await AuthenticationUtils.AuthenticateAsync(KsefClient, SignatureService, delegateNip)).AccessToken.Token;

        // Act: 2) Nadanie uprawnień pośrednich przez pośrednika
        PermissionsOperationStatusResponse indirectGrantStatus = await GrantIndirectPermissionsAsync();

        // Assert
        Assert.NotNull(indirectGrantStatus);
        Assert.Equal(OperationSuccessfulStatusCode, indirectGrantStatus.Status.Code);

        // Act: 3) Wyszukanie nadanych uprawnień (w bieżącym kontekście, nieaktywne)
        PagedPermissionsResponse<Client.Core.Models.Permissions.PersonPermission> permissionsAfterGrant = await SearchGrantedPersonPermissionsInCurrentContextAsync(
            accessToken: delegateAccessToken,
            includeInactive: true,
            pageOffset: 0,
            pageSize: 10);

        // Assert
        Assert.NotNull(permissionsAfterGrant);
        Assert.NotEmpty(permissionsAfterGrant.Permissions);

        await Task.Delay(SleepTime);

        // Act: 4) Cofnięcie nadanych uprawnień
        List<PermissionsOperationStatusResponse> revokeResult = await RevokePermissionsAsync(permissionsAfterGrant.Permissions, delegateAccessToken);
        
        // Assert
        Assert.NotNull(revokeResult);
        Assert.NotEmpty(revokeResult);
        Assert.Equal(permissionsAfterGrant.Permissions.Count, revokeResult.Count);
        Assert.All(revokeResult, r =>
            Assert.True(r.Status.Code == OperationSuccessfulStatusCode,
                $"Operacja cofnięcia uprawnień nie powiodła się: {r.Status.Description}, szczegóły: [{string.Join(",", r.Status.Details ?? Array.Empty<string>())}]")
        );

        await Task.Delay(SleepTime);

        // Act: 5) Wyszukanie po cofnięciu – brak uprawnień lub oczekiwana liczba pozostałych
        PagedPermissionsResponse<Client.Core.Models.Permissions.PersonPermission> permissionsAfterRevoke = await SearchGrantedPersonPermissionsInCurrentContextAsync(
            accessToken: delegateAccessToken,
            includeInactive: true,
            pageOffset: 0,
            pageSize: 10);

        // Assert
        Assert.NotNull(permissionsAfterRevoke);
        Assert.Empty(permissionsAfterRevoke.Permissions);
    }

    /// <summary>
    /// Nadaje uprawnienie CredentialsManage przez właściciela dla pośrednika
    /// </summary>
    /// <returns>Status operacji nadania uprawnień osobowych</returns>
    private async Task<PermissionsOperationStatusResponse> GrantCredentialsManageToDelegateAsync()
    {
        GrantPermissionsPersonRequest request = GrantPersonPermissionsRequestBuilder
            .Create()
            .WithSubject(
                new Client.Core.Models.Permissions.Person.SubjectIdentifier
                {
                    Type = Client.Core.Models.Permissions.Person.SubjectIdentifierType.Nip,
                    Value = delegateNip
                }
            )
            .WithPermissions(Client.Core.Models.Permissions.Person.StandardPermissionType.CredentialsManage)
            .WithDescription("E2E test - nadanie uprawnień CredentialsManage do zarządzania uprawnieniami")
            .Build();

        OperationResponse grantOperationResponse =
            await KsefClient.GrantsPermissionPersonAsync(request, ownerAccessToken, CancellationToken);

        Assert.NotNull(grantOperationResponse);
        Assert.False(string.IsNullOrEmpty(grantOperationResponse.OperationReferenceNumber));

        await Task.Delay(SleepTime);

        PermissionsOperationStatusResponse grantOperationStatus =
            await KsefClient.OperationsStatusAsync(grantOperationResponse.OperationReferenceNumber, ownerAccessToken);

        return grantOperationStatus;
    }

    /// <summary>
    /// Nadaje uprawnienia pośrednie dla podmiotu w kontekście wskazanego NIP przez pośrednika.
    /// </summary>
    /// <returns>Status operacji nadania uprawnień pośrednich</returns>
    private async Task<PermissionsOperationStatusResponse> GrantIndirectPermissionsAsync()
    {
        GrantPermissionsIndirectEntityRequest request = GrantIndirectEntityPermissionsRequestBuilder
            .Create()
            .WithSubject(Subject)
            .WithContext(
                new Client.Core.Models.Permissions.IndirectEntity.TargetIdentifier
                {
                    Type = Client.Core.Models.Permissions.IndirectEntity.TargetIdentifierType.Nip,
                    Value = ownerNip
                }
            )
            .WithPermissions(
                Client.Core.Models.Permissions.IndirectEntity.StandardPermissionType.InvoiceRead,
                Client.Core.Models.Permissions.IndirectEntity.StandardPermissionType.InvoiceWrite
            )
            .WithDescription("E2E test - przekazanie uprawnień (InvoiceRead, InvoiceWrite) przez pośrednika")
            .Build();

        OperationResponse grantOperationResponse =
            await KsefClient.GrantsPermissionIndirectEntityAsync(request, delegateAccessToken, CancellationToken);

        Assert.NotNull(grantOperationResponse);
        Assert.False(string.IsNullOrEmpty(grantOperationResponse.OperationReferenceNumber));

        await Task.Delay(SleepTime);

        PermissionsOperationStatusResponse grantOperationStatus =
            await GetOperationStatusAsync(grantOperationResponse.OperationReferenceNumber, delegateAccessToken);

        return grantOperationStatus;
    }

    /// <summary>
    /// Wyszukuje uprawnienia nadane osobom w bieżącym kontekście.
    /// Możliwe jest włączenie filtracji po stanie (np. nieaktywne).
    /// </summary>
    /// <returns>Zwraca stronicowaną listę uprawnień.</returns>
    private async Task<Client.Core.Models.Permissions.PagedPermissionsResponse<Client.Core.Models.Permissions.PersonPermission>>
        SearchGrantedPersonPermissionsInCurrentContextAsync(
            string accessToken,
            bool includeInactive,
            int pageOffset,
            int pageSize)
    {
        Client.Core.Models.Permissions.Person.PersonPermissionsQueryRequest query = new Client.Core.Models.Permissions.Person.PersonPermissionsQueryRequest
        {
            QueryType = Client.Core.Models.Permissions.Person.QueryTypeEnum.PermissionsGrantedInCurrentContext,
            PermissionState = includeInactive
                ? Client.Core.Models.Permissions.Person.PermissionState.Inactive
                : Client.Core.Models.Permissions.Person.PermissionState.Active
        };

        PagedPermissionsResponse<Client.Core.Models.Permissions.PersonPermission> pagedPermissionsResponse = await KsefClient.SearchGrantedPersonPermissionsAsync(
            query,
            accessToken,
            pageOffset: pageOffset,
            pageSize: pageSize,
            CancellationToken);
        return pagedPermissionsResponse;
    }

    /// <summary>
    /// Cofnięcie wszystkich przekazanych uprawnień i zwrócenie statusów operacji.
    /// </summary>
    private async Task<List<PermissionsOperationStatusResponse>> RevokePermissionsAsync(
        IEnumerable<Client.Core.Models.Permissions.PersonPermission> permissions,
        string accessToken)
    {
        List<KSeF.Client.Core.Models.Permissions.OperationResponse> revokeResponses = new List<KSeF.Client.Core.Models.Permissions.OperationResponse>();

        // Uruchomienie operacji cofania
        foreach (Client.Core.Models.Permissions.PersonPermission permission in permissions)
        {
            Client.Core.Models.Permissions.OperationResponse response = await KsefClient.RevokeCommonPermissionAsync(permission.Id, accessToken, CancellationToken.None);
            revokeResponses.Add(response);
        }

        // Sprawdzenie statusów wszystkich operacji
        List<Client.Core.Models.Permissions.PermissionsOperationStatusResponse> revokeStatusResults = new List<Client.Core.Models.Permissions.PermissionsOperationStatusResponse>();

        foreach (Client.Core.Models.Permissions.OperationResponse revokeResponse in revokeResponses)
        {
            await Task.Delay(SleepTime);
            Client.Core.Models.Permissions.PermissionsOperationStatusResponse status = await KsefClient.OperationsStatusAsync(revokeResponse.OperationReferenceNumber, accessToken);
            revokeStatusResults.Add(status);
        }

        return revokeStatusResults;
    }

    /// <summary>
    /// Zwraca status operacji na podstawie numeru referencyjnego.
    /// </summary>
    private async Task<Client.Core.Models.Permissions.PermissionsOperationStatusResponse> GetOperationStatusAsync(
        string operationReferenceNumber,
        string accessToken)
    {
        PermissionsOperationStatusResponse permissionsOperationStatusResponse = await KsefClient.OperationsStatusAsync(operationReferenceNumber, accessToken);
        return permissionsOperationStatusResponse;
    }
}