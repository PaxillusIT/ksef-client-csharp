using KSeF.Client.Api.Builders.PersonPermissions;
using KSeF.Client.Core.Models.Permissions.Person;
using KSeF.Client.Core.Models.Permissions;
using KSeF.Client.Tests.Utils;

namespace KSeF.Client.Tests.Core.E2E.Permissions.PersonPermission;


public class PersonPermissionE2ETests : TestBase
{
    private const string PermissionDescription = "E2E test grant";
    private const int OperationSuccessfulStatusCode = 200;

    // Zamiast fixture: prywatne readonly pola
    private string accessToken = string.Empty;
    private KSeF.Client.Core.Models.Permissions.Person.SubjectIdentifier Person { get; } = new();

    public PersonPermissionE2ETests()
    {
        Client.Core.Models.Authorization.AuthOperationStatusResponse auth = AuthenticationUtils
            .AuthenticateAsync(KsefClient, SignatureService)
            .GetAwaiter().GetResult();

        accessToken = auth.AccessToken.Token;

        // Ustaw dane osoby testowej (PESEL)
        Person.Value = MiscellaneousUtils.GetRandomPesel();
        Person.Type = SubjectIdentifierType.Pesel;
    }

    /// <summary>
    /// Testy E2E nadawania i cofania uprawnień dla osób:
    /// - nadanie uprawnień
    /// - wyszukanie nadanych uprawnień
    /// - cofnięcie uprawnień
    /// - ponowne wyszukanie (weryfikacja, że zostały cofnięte)
    /// </summary>
    [Fact]
    public async Task PersonPermissions_FullFlow_GrantSearchRevokeSearch()
    {
        // 1) Nadaj uprawnienia dla osoby
        OperationResponse grantResponse =
            await GrantPersonPermissionsAsync(Person, PermissionDescription, accessToken);

        Assert.NotNull(grantResponse);
        Assert.False(string.IsNullOrEmpty(grantResponse.OperationReferenceNumber));

        await Task.Delay(SleepTime);

        // 2) Wyszukaj nadane uprawnienia — powinny być widoczne
        PagedPermissionsResponse<Client.Core.Models.Permissions.PersonPermission> searchAfterGrant =
            await SearchGrantedPersonPermissionsAsync(accessToken);

        Assert.NotNull(searchAfterGrant);
        Assert.NotEmpty(searchAfterGrant.Permissions);

        // Zawężenie do uprawnień nadanych w tym teście po opisie
        List<KSeF.Client.Core.Models.Permissions.PersonPermission> grantedNow =
            searchAfterGrant.Permissions
                .Where(p => p.Description == PermissionDescription)
                .ToList();

        Assert.NotEmpty(grantedNow);
        Assert.Contains(grantedNow, x => Enum.Parse<StandardPermissionType>(x.PermissionScope) == StandardPermissionType.InvoiceRead);
        Assert.Contains(grantedNow, x => Enum.Parse<StandardPermissionType>(x.PermissionScope) == StandardPermissionType.InvoiceWrite);

        await Task.Delay(SleepTime);

        // 3) Cofnij nadane uprawnienia
        List<PermissionsOperationStatusResponse> revokeResult = await RevokePermissionsAsync(searchAfterGrant.Permissions, accessToken);

        Assert.NotNull(revokeResult);
        Assert.NotEmpty(revokeResult);
        Assert.Equal(searchAfterGrant.Permissions.Count, revokeResult.Count);
        Assert.All(revokeResult, r =>
            Assert.True(r.Status.Code == OperationSuccessfulStatusCode,
                $"Operacja cofnięcia uprawnień nie powiodła się: {r.Status.Description}, szczegóły: [{string.Join(",", r.Status.Details ?? Array.Empty<string>())}]")
        );

        await Task.Delay(SleepTime);

        // 4) Wyszukaj ponownie — po cofnięciu nie powinno być wpisów (lub zgodnie z oczekiwaniem po błędach cofania)
        PagedPermissionsResponse<Client.Core.Models.Permissions.PersonPermission> searchAfterRevoke =
            await SearchGrantedPersonPermissionsAsync(accessToken);

        Assert.NotNull(searchAfterRevoke);

        List<KSeF.Client.Core.Models.Permissions.PersonPermission> remaining =
            searchAfterRevoke.Permissions
                .Where(p => p.Description == PermissionDescription)
                .ToList();

        Assert.Empty(remaining);
    }

    /// <summary>
    /// Nadaje uprawnienia dla osoby i zwraca odpowiedź operacji.
    /// </summary>
    private async Task<OperationResponse> GrantPersonPermissionsAsync(
        Client.Core.Models.Permissions.Person.SubjectIdentifier subject,
        string description,
        string accessToken)
    {
        GrantPermissionsPersonRequest request = GrantPersonPermissionsRequestBuilder
            .Create()
            .WithSubject(subject)
            .WithPermissions(
                StandardPermissionType.InvoiceRead,
                StandardPermissionType.InvoiceWrite)
            .WithDescription(description)
            .Build();

        OperationResponse response =
            await KsefClient.GrantsPermissionPersonAsync(request, accessToken, CancellationToken);

        return response;
    }

    /// <summary>
    /// Wyszukuje nadane uprawnienia dla osób i zwraca wynik wyszukiwania.
    /// </summary>
    private async Task<PagedPermissionsResponse<Client.Core.Models.Permissions.PersonPermission>>
        SearchGrantedPersonPermissionsAsync(string accessToken)
    {
        PersonPermissionsQueryRequest query = new PersonPermissionsQueryRequest
        {
            PermissionTypes = new List<PersonPermissionType>
            {
                PersonPermissionType.InvoiceRead,
                PersonPermissionType.InvoiceWrite
            }
        };

        PagedPermissionsResponse<Client.Core.Models.Permissions.PersonPermission> response =
            await KsefClient.SearchGrantedPersonPermissionsAsync(query, accessToken, pageOffset: 0, pageSize: 10, CancellationToken);

        return response;
    }

    /// <summary>
    /// Cofnięcie wszystkich przekazanych uprawnień i zwrócenie statusów operacji.
    /// </summary>
    private async Task<List<Client.Core.Models.Permissions.PermissionsOperationStatusResponse>> RevokePermissionsAsync(
        IEnumerable<KSeF.Client.Core.Models.Permissions.PersonPermission> grantedPermissions,
        string accessToken)
    {
        List<KSeF.Client.Core.Models.Permissions.OperationResponse> revokeResponses = new List<Client.Core.Models.Permissions.OperationResponse>();

        // Uruchomienie operacji cofania
        foreach (Client.Core.Models.Permissions.PersonPermission permission in grantedPermissions)
        {
            Client.Core.Models.Permissions.OperationResponse response = await KsefClient.RevokeCommonPermissionAsync(permission.Id, accessToken, CancellationToken.None);
            revokeResponses.Add(response);
        }

        // Sprawdzenie statusów wszystkich operacji
        List<Client.Core.Models.Permissions.PermissionsOperationStatusResponse> statuses = new List<Client.Core.Models.Permissions.PermissionsOperationStatusResponse>();

        foreach (Client.Core.Models.Permissions.OperationResponse revokeResponse in revokeResponses)
        {
            await Task.Delay(SleepTime);
            Client.Core.Models.Permissions.PermissionsOperationStatusResponse status = await KsefClient.OperationsStatusAsync(revokeResponse.OperationReferenceNumber, accessToken);
            statuses.Add(status);
        }

        return statuses;
    }
}