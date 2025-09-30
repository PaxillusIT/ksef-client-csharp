namespace KSeF.Client.Core.Models.Token;

public sealed class PersonToken
{
    public string Issuer { get; init; }
    public string[] Audiences { get; init; } = Array.Empty<string>();
    public DateTimeOffset? IssuedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }

    public string[] Roles { get; init; } = Array.Empty<string>();

    public string TokenType { get; init; }
    public string ContextIdType { get; init; }
    public string ContextIdValue { get; init; }
    public string AuthMethod { get; init; }
    public string AuthRequestNumber { get; init; }
    public TokenSubjectDetails? SubjectDetails { get; init; }
    public string[] Permissions { get; init; } = Array.Empty<string>();
    public string[] PermissionsExcluded { get; init; } = Array.Empty<string>();
    public string[] RolesRaw { get; init; } = Array.Empty<string>();
    public string[] PermissionsEffective { get; init; } = Array.Empty<string>();
    public TokenIppPolicy IpPolicy { get; init; }
}
