using KSeF.Client.Core.Models.Permissions.Person;

namespace KSeF.Client.Core.Models.Permissions
{
    public class PersonalPermission
    {
        public string Id { get; set; }
        public ContextIdentifier ContextIdentifier { get; set; }
        public AuthorizedIdentifier AuthorizedIdentifier { get; set; }
        public TargetIdentifier TargetIdentifier { get; set; }
        public PersonPermissionType PermissionScope { get; set; }
        public string Description { get; set; }
        public PermissionState PermissionState { get; set; }
        public DateTimeOffset StartDate { get; set; }
        public bool CanDelegate { get; set; }
    }
}
