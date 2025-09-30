using KSeF.Client.Core.Models.Permissions.Person;

namespace KSeF.Client.Core.Models.Permissions;

public class PersonPermission
{
    public string Id { get; set; }
    public AuthorizedIdentifier AuthorizedIdentifier { get; set; }
    public ContextIdentifier ContextIdentifier { get; set; }
    public TargetIdentifier TargetIdentifier { get; set; }
    public AuthorIdentifier AuthorIdentifier { get; set; }
    public string PermissionScope { get; set; }
    public string Description { get; set; }
    public string PermissionState { get; set; }
    public DateTime StartDate { get; set; }
    public bool CanDelegate { get; set; }
}

public class SubunitPermission
{
    public string Id { get; set; }
    public AuthorizedIdentifier AuthorizedIdentifier { get; set; }
    public SubjectIdentifier SubjectIdentifier { get; set; }
    public AuthorIdentifier AuthorIdentifier { get; set; }
    public SubunitPermissionType PermissionScope { get; set; }
    public string Description { get; set; }
    public DateTimeOffset StartDate { get; set; }
}

public enum SubunitPermissionType
{
    CredentialsManage
}

public class SubjectIdentifier
{
    public SubunitIdentifierType Type { get; set; }
    public string Value { get; set; }
}
public enum SubunitIdentifierType
{
    InternalId,
    Nip
}

public class EntityRole
{
    public ContextIdentifier ParentEntityIdentifier { get; set; }
    public string Role { get; set; }
    public string Description { get; set; }
    public DateTime StartDate { get; set; }
}

public class SubordinateEntityRole
{
    public string SubordinateEntityIdentifier { get; set; }
    public string SubordinateEntityIdentifierType { get; set; }
    public string Role { get; set; }
    public string Description { get; set; }
    public DateTime StartDate { get; set; }
}

public class EuEntityPermission
{
    public string Id { get; set; }
    public AuthorIdentifier AuthorIdentifier { get; set; }
    public string VatUeIdentifier { get; set; }
    public string EuEntityName { get; set; }
    public string AuthorizedFingerprintIdentifier { get; set; }
    public string PermissionScope { get; set; }
    public string Description { get; set; }
    public DateTime StartDate { get; set; }
}

public class AuthorizationGrant
{
    public string Id { get; set; }
    public AuthorIdentifier AuthorIdentifier { get; set; }
    public AuthorizedEntityIdentifier AuthorizedEntityIdentifier { get; set; }
    public AuthorizingEntityIdentifier AuthorizingEntityIdentifier { get; set; }
    public string AuthorizationScope { get; set; }
    public string Description { get; set; }
    public DateTime StartDate { get; set; }
}

public class AuthorIdentifier
{
    public AuthorIdentifierType Type { get; set; }
    public string Value { get; set; }
}

public enum AuthorIdentifierType
{
    Nip,
    Pesel,
    Fingerprint,
}
public class AuthorizedEntityIdentifier
{
    public AuthorizedEntityIdentifierType Type { get; set; }
    public string Value { get; set; }
}

public enum AuthorizedEntityIdentifierType
{
    Nip,
    PeppolId
}

public class AuthorizingEntityIdentifier
{
    public AuthorizingEntityIdentifierType Type { get; set; }
    public string Value { get; set; }
}

public enum AuthorizingEntityIdentifierType
{
    Nip
}