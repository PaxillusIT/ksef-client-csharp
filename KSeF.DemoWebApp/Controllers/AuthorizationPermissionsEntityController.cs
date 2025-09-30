using KSeF.Client.Api.Builders.AuthorizationPermissions;
using KSeF.Client.Core.Models.Permissions;
using KSeF.Client.Core.Models.Permissions.AuthorizationEntity;
using KSeF.Client;
using Microsoft.AspNetCore.Mvc;

namespace KSeF.DemoWebApp.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthorizationPermissionsEntityController(IKSeFClient ksefClient) : ControllerBase
{
    [HttpPost("grant-authorization-permissions-for-entity")]
    public async Task<ActionResult<OperationResponse>> GrantPermissionsEntity(string accessToken, Client.Core.Models.Permissions.AuthorizationEntity.SubjectIdentifier subjectIdentifier, CancellationToken cancellationToken)
    {
        var request = GrantAuthorizationPermissionsRequestBuilder
            .Create()
            .WithSubject(subjectIdentifier)
            .WithPermission(StandardPermissionType.TaxRepresentative)
            .WithDescription("Access for quarterly review")
            .Build();

        return await ksefClient.GrantsAuthorizationPermissionAsync(request, accessToken, cancellationToken);
    }

    [HttpPost("revoke-authorization-permissions-for-entity")]
    public async Task<ActionResult<OperationResponse>> RevokePermissionsEntity(
    string accessToken,
    string permissionId,
    CancellationToken cancellationToken)
    {

        return await ksefClient.RevokeAuthorizationsPermissionAsync(permissionId, accessToken, cancellationToken);
    }
}
