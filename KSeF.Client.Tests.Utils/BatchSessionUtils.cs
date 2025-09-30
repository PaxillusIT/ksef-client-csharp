using KSeF.Client.Core.Interfaces;
using KSeF.Client.Core.Models.Invoices;
using KSeF.Client.Core.Models.Sessions;
using KSeF.Client.Core.Models.Sessions.BatchSession;
using System.IO.Compression;
using System.Text;

namespace KSeF.Client.Tests.Utils;

/// <summary>
/// Zawiera metody pomocnicze do obsługi sesji wsadowych w systemie KSeF.
/// </summary>
public static class BatchUtils
{
    private const SystemCodeEnum DefaultSystemCode = SystemCodeEnum.FA3;
    private const string DefaultSchemaVersion = "1-0E";
    private const string DefaultValue = "FA";
    private const int DefaultSleepTimeMs = 1000;
    private const int DefaultMaxAttempts = 60;
    private const int DefaultPageOffset = 0;
    private const int DefaultPageSize = 10;

    /// <summary>
    /// Pobiera metadane faktur przesłanych w ramach sesji wsadowej.
    /// </summary>
    /// <param name="ksefClient">Klient KSeF.</param>
    /// <param name="sessionReferenceNumber">Numer referencyjny sesji.</param>
    /// <param name="accessToken">Token dostępu.</param>
    /// <param name="pageOffset">Numer strony wyników.</param>
    /// <param name="pageSize">Rozmiar strony wyników.</param>
    /// <returns>Odpowiedź z metadanymi faktur sesji.</returns>
    public static async Task<SessionInvoicesResponse> GetSessionInvoicesAsync(
        IKSeFClient ksefClient,
        string sessionReferenceNumber,
        string accessToken,
        int pageOffset = DefaultPageOffset, int pageSize = DefaultPageSize)
        => await ksefClient.GetSessionInvoicesAsync(sessionReferenceNumber, accessToken, pageSize);

    /// <summary>
    /// Pobiera UPO dla faktury z sesji wsadowej na podstawie numeru KSeF.
    /// </summary>
    /// <param name="ksefClient">Klient KSeF.</param>
    /// <param name="sessionReferenceNumber">Numer referencyjny sesji.</param>
    /// <param name="ksefNumber">Numer KSeF faktury.</param>
    /// <param name="accessToken">Token dostępu.</param>
    /// <returns>UPO w formacie XML.</returns>
    public static async Task<string> GetSessionInvoiceUpoByKsefNumberAsync(
        IKSeFClient ksefClient,
        string sessionReferenceNumber,
        string ksefNumber,
        string accessToken)
        => await ksefClient.GetSessionInvoiceUpoByKsefNumberAsync(sessionReferenceNumber, ksefNumber, accessToken);

    /// <summary>
    /// Otwiera nową sesję wsadową w systemie KSeF.
    /// </summary>
    /// <param name="client">Klient KSeF.</param>
    /// <param name="openReq">Żądanie otwarcia sesji wsadowej.</param>
    /// <param name="accessToken">Token dostępu.</param>
    /// <returns>Odpowiedź z informacjami o otwartej sesji wsadowej.</returns>
    public static async Task<OpenBatchSessionResponse> OpenBatchAsync(
        IKSeFClient client,
        OpenBatchSessionRequest openReq,
        string accessToken)
        => await client.OpenBatchSessionAsync(openReq, accessToken);

    /// <summary>
    /// Wysyła części paczki faktur w ramach otwartej sesji wsadowej.
    /// </summary>
    /// <param name="client">Klient KSeF.</param>
    /// <param name="openResp">Odpowiedź z otwarcia sesji wsadowej.</param>
    /// <param name="parts">Kolekcja partów do wysłania.</param>
    public static async Task SendBatchPartsAsync(
        IKSeFClient client,
        OpenBatchSessionResponse openResp,
        ICollection<BatchPartSendingInfo> parts)
        => await client.SendBatchPartsAsync(openResp, parts);

    /// <summary>
    /// Zamyka sesję wsadową w systemie KSeF.
    /// </summary>
    /// <param name="client">Klient KSeF.</param>
    /// <param name="sessionReferenceNumber">Numer referencyjny sesji.</param>
    /// <param name="accessToken">Token dostępu.</param>
    public static async Task CloseBatchAsync(
        IKSeFClient client,
        string sessionReferenceNumber,
        string accessToken)
        => await client.CloseBatchSessionAsync(sessionReferenceNumber, accessToken);

    /// <summary>
    /// Buduje ZIP w pamięci z podanych plików i zwraca bajty oraz metadane (przed szyfrowaniem).
    /// </summary>
    /// <param name="files">Kolekcja plików do spakowania.</param>
    /// <param name="crypto">Serwis kryptograficzny.</param>
    /// <returns>Krotka: bajty ZIP oraz metadane pliku.</returns>
    public static (byte[] ZipBytes, FileMetadata Meta) BuildZip(
        IEnumerable<(string FileName, byte[] Content)> files,
        ICryptographyService crypto)
    {
        using var zipStream = new MemoryStream();
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true);
        
        foreach (var (fileName, content) in files)
        {
            var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            entryStream.Write(content);
        }
        
        archive.Dispose();
        
        var zipBytes = zipStream.ToArray();
        var meta = crypto.GetMetaData(zipBytes);

        return (zipBytes, meta);
    }

    /// <summary>
    /// Dzieli bufor na określoną liczbę części o zbliżonym rozmiarze.
    /// </summary>
    /// <param name="input">Bufor wejściowy.</param>
    /// <param name="partCount">Liczba części.</param>
    /// <returns>Lista buforów podzielonych na części.</returns>
    public static List<byte[]> Split(byte[] input, int partCount)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(partCount);

        var result = new List<byte[]>(partCount);
        var partSize = (int)Math.Ceiling((double)input.Length / partCount);

        for (int i = 0; i < partCount; i++)
        {
            var start = i * partSize;
            var size = Math.Min(partSize, input.Length - start);
            if (size <= 0) break;

            var part = new byte[size];
            Array.Copy(input, start, part, 0, size);
            result.Add(part);
        }

        return result;
    }

    /// <summary>
    /// Szyfruje i pakuje do struktur partów (1..N). Gdy <paramref name="partCount"/> == 1, nie dzieli <paramref name="zipBytes"/> na części.
    /// </summary>
    /// <param name="zipBytes">Bajty ZIP do podziału i zaszyfrowania.</param>
    /// <param name="encryption">Dane szyfrowania.</param>
    /// <param name="crypto">Serwis kryptograficzny.</param>
    /// <param name="partCount">Liczba części.</param>
    /// <returns>Lista zaszyfrowanych partów do wysyłki.</returns>
    public static List<BatchPartSendingInfo> EncryptAndSplit(
        byte[] zipBytes,
        EncryptionData encryption,
        ICryptographyService crypto,
        int partCount = 1)
    {
        ArgumentNullException.ThrowIfNull(zipBytes);
        ArgumentNullException.ThrowIfNull(encryption);
        ArgumentNullException.ThrowIfNull(crypto);

        var rawParts = partCount <= 1
            ? new List<byte[]> { zipBytes }
            : Split(zipBytes, partCount);

        var result = new List<BatchPartSendingInfo>(rawParts.Count);

        for (int i = 0; i < rawParts.Count; i++)
        {
            var encrypted = crypto.EncryptBytesWithAES256(rawParts[i], encryption.CipherKey, encryption.CipherIv);
            var meta = crypto.GetMetaData(encrypted);

            result.Add(new BatchPartSendingInfo
            {
                Data = encrypted,
                OrdinalNumber = i + 1,
                Metadata = meta
            });
        }

        return result;
    }

    /// <summary>
    /// Buduje żądanie otwarcia sesji wsadowej z kodem formularza i listą zaszyfrowanych partów.
    /// </summary>
    /// <param name="zipMeta">Metadane pliku ZIP.</param>
    /// <param name="encryption">Dane szyfrowania.</param>
    /// <param name="encryptedParts">Lista zaszyfrowanych partów.</param>
    /// <param name="systemCode">Kod systemowy formularza.</param>
    /// <param name="schemaVersion">Wersja schematu.</param>
    /// <param name="value">Wartość formularza.</param>
    /// <returns>Obiekt żądania otwarcia sesji wsadowej.</returns>
    public static OpenBatchSessionRequest BuildOpenBatchRequest(
        FileMetadata zipMeta,
        EncryptionData encryption,
        IEnumerable<BatchPartSendingInfo> encryptedParts,
        SystemCodeEnum systemCode = DefaultSystemCode,
        string schemaVersion = DefaultSchemaVersion,
        string value = DefaultValue)
    {
        var builder = OpenBatchSessionRequestBuilder
            .Create()
            .WithFormCode(systemCode: SystemCodeHelper.GetSystemCode(systemCode), schemaVersion: schemaVersion, value: value)
            .WithBatchFile(fileSize: zipMeta.FileSize, fileHash: zipMeta.HashSHA);

        foreach (var p in encryptedParts)
        {
            builder = builder.AddBatchFilePart(
                ordinalNumber: p.OrdinalNumber,
                fileName: $"part_{p.OrdinalNumber}.zip.aes",
                fileSize: p.Metadata.FileSize,
                fileHash: p.Metadata.HashSHA);
        }

        return builder
            .EndBatchFile()
            .WithEncryption(
                encryptedSymmetricKey: encryption.EncryptionInfo.EncryptedSymmetricKey,
                initializationVector: encryption.EncryptionInfo.InitializationVector)
            .Build();
    }

    /// <summary>
    /// Sprawdza status sesji wsadowej aż do przetworzenia lub przekroczenia limitu prób.
    /// </summary>
    /// <param name="client">Klient KSeF.</param>
    /// <param name="sessionRef">Numer referencyjny sesji.</param>
    /// <param name="accessToken">Token dostępu.</param>
    /// <param name="sleepTime">Czas oczekiwania pomiędzy próbami (ms).</param>
    /// <param name="maxAttempts">Maksymalna liczba prób.</param>
    /// <returns>Odpowiedź ze statusem sesji.</returns>
    public static async Task<SessionStatusResponse> WaitForBatchStatusAsync(
        IKSeFClient client,
        string sessionRef,
        string accessToken,
        int sleepTime = DefaultSleepTimeMs,
        int maxAttempts = DefaultMaxAttempts)
    {
        SessionStatusResponse sessionStatus = null!;
        for (int i = 0; i < maxAttempts; i++)
        {
            sessionStatus = await client.GetSessionStatusAsync(sessionRef, accessToken);

            if (sessionStatus.Status.Code != 150) // W trakcie przetwrzania
            {
                return sessionStatus;
            }

            await Task.Delay(sleepTime);
        }

        return sessionStatus;
    }

    /// <summary>
    /// Generuje dokumenty XML w pamięci na podstawie szablonu i NIP.
    /// </summary>
    /// <param name="count">Liczba dokumentów do wygenerowania.</param>
    /// <param name="nip">NIP podmiotu.</param>
    /// <param name="templatePath">Ścieżka do pliku szablonu XML.</param>
    /// <param name="invoiceNumberFactory">Funkcja generująca numer faktury (opcjonalnie).</param>
    /// <returns>Lista krotek: nazwa pliku i zawartość w bajtach.</returns>
    public static List<(string FileName, byte[] Content)> GenerateInvoicesInMemory(
        int count,
        string nip,
        string templatePath,
        Func<string>? invoiceNumberFactory = null)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Templates", templatePath);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Template not found at: {path}");

        var template = File.ReadAllText(path, Encoding.UTF8);

        List<(string FileName, byte[] Content)> list = new(count);

        for (int i = 0; i < count; i++)
        {
            var xml = template
                .Replace("#nip#", nip)
                .Replace("#invoice_number#", (invoiceNumberFactory?.Invoke() ?? Guid.NewGuid().ToString()));
            list.Add(($"faktura_{i + 1}.xml", Encoding.UTF8.GetBytes(xml)));
        }

        return list;
    }
}