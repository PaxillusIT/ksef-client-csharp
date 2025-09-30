using KSeF.Client.Core.Models.Sessions.ActiveSessions;

namespace KSeF.Client.Core.Models;

public class StatusInfo
{
    public int Code { get; set; }
    public string Description { get; set; }
    public ICollection<string> Details { get; set; }
}

public class AuthStatus
{
    public DateTime StartDate { get; set; }
    public AuthenticationMethodEnum AuthenticationMethod { get; set; }
    public StatusInfo Status { get; set; }
}