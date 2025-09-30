using KSeF.Client.Core.Models.Permissions;
using KSeF.Client.Core.Models.Permissions.SubUnit;
using KSeF.Client.Tests.Utils;

namespace KSeF.Client.Tests.Core.E2E.Permissions.SubunitPermission;

public class SubunitPermissionsScenarioE2EFixture
{
    public ContextIdentifier Unit { get; } = new ContextIdentifier
    {
        Type = ContextIdentifierType.Nip,
        Value = MiscellaneousUtils.GetRandomNip()
    };

    public string UnitNipInternal { get; set; }

    public ContextIdentifier Subunit { get; } = new ContextIdentifier
    {
        Type = ContextIdentifierType.Nip,
        Value = MiscellaneousUtils.GetRandomNip()
    };

    public Client.Core.Models.Permissions.SubUnit.SubjectIdentifier SubjectIdentifier { get; } = new Client.Core.Models.Permissions.SubUnit.SubjectIdentifier
    {
        Type = SubjectIdentifierType.Nip,
        Value = MiscellaneousUtils.GetRandomNip()
    };

    public OperationResponse GrantResponse { get; set; }
    public List<PermissionsOperationStatusResponse> RevokeStatusResults { get; set; } = new();
    public PagedPermissionsResponse<Client.Core.Models.Permissions.SubunitPermission> SearchResponse { get; internal set; }
}
