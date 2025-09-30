using System.Security.Cryptography.X509Certificates;
using System.Xml;

namespace KSeF.Client.Core.Interfaces;

/// <summary>
/// Interfejs definiujący usługę podpisu elektronicznego 
/// w formacie XAdES na potrzeby procesu uwierzytelniania KSeF.
/// </summary>
public interface ISignatureService
{
    /// <summary>
    /// Podpisuje wskazany dokument XML w formacie XAdES, 
    /// używając dostarczonego certyfikatu z kluczem prywatnym.
    /// </summary>
    /// <param name="xml">
    /// Dokument XML (AuthTokenRequest) w formie tekstowej.
    /// </param>
    /// <param name="certificate">
    /// Certyfikat X.509 zawierający klucz prywatny, 
    /// którym ma zostać złożony podpis.
    /// </param>
    /// <returns>
    /// Dokument XML podpisany w formacie XAdES (string).
    /// </returns>
    string Sign(string xml, X509Certificate2 certificate);

    /// <summary>
    /// Podpisuje wskazany dokument XML w formacie XAdES, 
    /// używając dostarczonego certyfikatu z kluczem prywatnym.
    /// Metoda modyfikuje przekazany dokument w miejscu, dodając element podpisu.
    /// </summary>
    /// <param name="xmlDocument">
    /// Dokument XML do podpisania. Musi posiadać element główny.
    /// Zalecane jest ustawienie PreserveWhitespace = true.
    /// </param>
    /// <param name="certificate">
    /// Certyfikat X.509 zawierający klucz prywatny, 
    /// którym ma zostać złożony podpis.
    /// </param>
    /// <returns>
    /// Ten sam obiekt XmlDocument co przekazany w parametrze, 
    /// zmodyfikowany przez dodanie elementu podpisu XAdES.
    /// </returns>
    XmlDocument Sign(XmlDocument xmlDocument, X509Certificate2 certificate);
}
