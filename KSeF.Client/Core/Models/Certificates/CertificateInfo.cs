namespace KSeF.Client.Core.Models.Certificates;

public class CertificateInfo
{
    public string CertificateSerialNumber { get; set; }
    public string Name { get; set; }
    public CertificateType CertificateType { get; set; }
    public string CommonName { get; set; }
    public string Status { get; set; }
    public SubjectIdentifier SubjectIdentifier { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ValidTo { get; set; }
    public DateTimeOffset LastUseDate { get; set; }
}
