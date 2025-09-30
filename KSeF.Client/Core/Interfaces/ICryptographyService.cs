using KSeF.Client.Core.Models.Certificates;
using KSeF.Client.Core.Models.Sessions;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace KSeF.Client.Core.Interfaces;

/// <summary>
/// Zarządza operacjami kryptograficznymi, takimi jak szyfrowanie danych, generowanie żądań podpisu certyfikatu (CSR) oraz metadanych plików.
/// </summary>
public interface ICryptographyService
{
    /// <summary>
    /// Zwraca dane szyfrowania, w tym klucz szyfrowania, wektor IV i zaszyfrowany klucz.
    /// </summary>
    /// <returns><see cref="EncryptionData"/></returns>
    EncryptionData GetEncryptionData();

    /// <summary>
    /// Szyfrowanie danych przy użyciu AES-256 w trybie CBC z PKCS7.
    /// </summary>
    /// <param name="content">Plik w formie byte array.</param>
    /// <param name="key">Klucz symetryczny.</param>
    /// <param name="iv">Wektor IV klucza symetrycznego.</param>
    /// <returns>Zaszyfrowany plik w formie byte array.</returns>
    byte[] EncryptBytesWithAES256(byte[] content, byte[] key, byte[] iv);

    /// <summary>
    /// Szyfrowanie danych przy użyciu AES-256 w trybie CBC z PKCS7.
    /// </summary>
    /// <param name="input">Input stream - niezaszyfrowany.</param>
    /// <param name="output">Output stream - zaszyfrowany.</param>
    /// <param name="key">Klucz symetryczny.</param>
    /// <param name="iv">Wektro IV klucza symetrycznego.</param>
    /// <returns>Zaszyfrowany plik w formie stream.</returns>
    void EncryptStreamWithAES256(Stream input, Stream output, byte[] key, byte[] iv);

    /// <summary>
    /// Asynchroniczne szyfrowanie danych przy użyciu AES-256 w trybie CBC z PKCS7.
    /// </summary>
    /// <param name="input">Input stream - niezaszyfrowany.</param>
    /// <param name="output">Output stream - zaszyfrowany.</param>
    /// <param name="key">Klucz symetryczny.</param>
    /// <param name="iv">Wektor IV klucza symetrycznego.</param>
    /// <param name="ct">Token anulowania.</param>
    Task EncryptStreamWithAES256Async(Stream input, Stream output, byte[] key, byte[] iv, CancellationToken ct = default);

    /// <summary>
    /// Generuje żądanie podpisania certyfikatu (CSR) z użyciem RSA na podstawie przekazanych informacji o certyfikacie.
    /// </summary>
    /// <param name="certificateInfo"><see cref="CertificateEnrollmentsInfoResponse"/></param>
    /// <param name="padding">Padding Pss jeżeli niepodany.</param>
    /// <returns>Zwraca CSR oraz klucz prywatny, oba zakodowane w Base64 w formacie DER</returns>
    (string, string) GenerateCsrWithRSA(CertificateEnrollmentsInfoResponse certificateInfo, RSASignaturePadding padding = null);

    /// <summary>
    /// Generuje żądanie podpisania certyfikatu (CSR) z użyciem krzywej eliptycznej (EC) na podstawie przekazanych informacji o certyfikacie.
    /// </summary>
    /// <param name="certificateInfo"></param>
    /// <returns>Zwraca CSR oraz klucz prywatny, oba zakodowane w Base64</returns>
    (string, string) GenerateCsrWithECDSA(CertificateEnrollmentsInfoResponse certificateInfo);

    /// <summary>
    /// Zwraca metadane plik: rozmiar i hash SHA256.
    /// </summary>
    /// <param name="file">Plik w formie byte array</param>
    /// <returns><see cref="FileMetadata"/></returns>
    FileMetadata GetMetaData(byte[] file);

    /// <summary>
    /// Zwraca zaszyfrowany plik formie byte array przy użyciu algorytmu RSA.
    /// </summary>
    /// <param name="content"></param>
    /// <param name="padding"></param>
    /// <returns></returns>
    byte[] EncryptWithRSAUsingPublicKey(byte[] content, RSAEncryptionPadding padding);

    /// <summary>
    /// Zwraca zaszyfrowany token KSeF przy użyciu algorytmu RSA z publicznym kluczem.
    /// </summary>
    /// <param name="content"></param>
    /// <returns></returns>
    byte[] EncryptKsefTokenWithRSAUsingPublicKey(byte[] content);

    /// <summary>
    /// Zwraca zaszyfrowany token KSeF przy użyciu algorytmu ECIes z publicznym kluczem.
    /// </summary>
    /// <param name="content"></param>
    /// <returns></returns>
    byte[] EncryptWithECDSAUsingPublicKey(byte[] content);

    /// <summary>
    /// Jednorazowe, asynchroniczne wstępne załadowanie certyfikatów i kluczy do pamięci podręcznej.
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task WarmupAsync(CancellationToken ct = default);

    /// <summary>
    /// Wymusza odświeżenie certyfikatów i kluczy w pamięci podręcznej.
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task ForceRefreshAsync(CancellationToken ct = default);

    /// <summary>
    /// Certyfikat używany do szyfrowania symetrycznego klucza AES.
    /// </summary>
    X509Certificate2 SymmetricKeyCertificate { get; }

    /// <summary>
    /// Certyfikat używany do szyfrowania tokena KSeF.
    /// </summary>
    X509Certificate2 KsefTokenCertificate { get; }
}