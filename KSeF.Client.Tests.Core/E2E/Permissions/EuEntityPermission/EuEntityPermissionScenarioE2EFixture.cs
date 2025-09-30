using KSeF.Client.Core.Models.Permissions;
using KSeF.Client.Core.Models.Permissions.EUEntity;
using KSeF.Client.Tests.Utils;

namespace KSeF.Client.Tests.Core.E2E.Permissions.EuEntityPermission;

public class EuEntityPermissionScenarioE2EFixture
{
    public string AccessToken { get; set; }
    public Client.Core.Models.Permissions.EUEntity.SubjectIdentifier EuEntity { get; } = new Client.Core.Models.Permissions.EUEntity.SubjectIdentifier
    {
        Type = SubjectIdentifierType.Fingerprint,
        Value = MiscellaneousUtils.GetRandomNip()
    };
    public OperationResponse GrantResponse { get; set; }
    public List<PermissionsOperationStatusResponse> RevokeStatusResults { get; set; } = new List<PermissionsOperationStatusResponse>();
    public PagedPermissionsResponse<Client.Core.Models.Permissions.EuEntityPermission> SearchResponse { get; set; }
    public string NipVatUe { get; set; }
}
