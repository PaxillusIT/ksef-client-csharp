namespace KSeF.Client.Core.Models.Permissions.Person;

public class PersonPermissionsQueryRequest
{
    public PersonPermissionsAuthorIdentifier AuthorIdentifier { get; set; }
    public PersonPermissionsAuthorizedIdentifier AuthorizedIdentifier { get; set; }
    public ContextIdentifier ContextIdentifier { get; set; }
    public PersonPermissionsTargetIdentifier TargetIdentifier { get; set; }
    public List<PersonPermissionType> PermissionTypes { get; set; }
    public PermissionState PermissionState { get; set; }
    public QueryTypeEnum QueryType { get; set; }
}

public class PersonPermissionsAuthorIdentifier
{
    public SubjectIdentifierType Type { get; set; }
    public string Value { get; set; }
}
public class PersonPermissionsAuthorizedIdentifier
{
    public AuthorizedIdentifierType Type { get; set; }
    public string Value { get; set; }
}
public class PersonPermissionsTargetIdentifier
{
    public TargetIdentifierType Type { get; set; }
    public string Value { get; set; }
}

public enum PersonPermissionType
{
    CredentialsManage,
    CredentialsRead,
    InvoiceWrite,
    InvoiceRead,
    Introspection,
    SubunitManage,
    EnforcementOperations,
    VatUeManage,
    Owner
}
public enum PermissionState
{
    Active,
    Inactive
}
public enum QueryTypeEnum
{
    PermissionsInCurrentContext,
    PermissionsGrantedInCurrentContext
}
