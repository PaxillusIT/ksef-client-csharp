using KSeF.Client.Core.Interfaces;
using KSeF.Client.Core.Models.Invoices;
using KSeF.Client.Tests.Utils;
using KSeF.Client.Tests.Utils.Upo;
using KSeF.Client.Core.Models.Sessions;

namespace KSeF.Client.Tests.Core.E2E.BatchSession;

[Collection("BatchSessionScenario")]
public partial class BatchSessionE2ETests : TestBase
{
    private const int TotalInvoices = 20;
    private const int PartQuantity = 11;
    private const int ExpectedFailedInvoiceCount = 0;
    private const int ExpectedSessionStatusCode = 200;

    private string accessToken = string.Empty;
    private string sellerNip = string.Empty;

    private string? batchSessionReferenceNumber;
    private string? ksefNumber;
    private string? upoReferenceNumber;
    private Client.Core.Models.Sessions.BatchSession.OpenBatchSessionResponse? openBatchSessionResponse;
    private List<Client.Core.Models.Sessions.BatchSession.BatchPartSendingInfo>? encryptedParts;

    public BatchSessionE2ETests()
    {
        // Autoryzacja do testów – jednorazowa, dane zapisane w readonly properties
        string nip = MiscellaneousUtils.GetRandomNip();
        Client.Core.Models.Authorization.AuthOperationStatusResponse authInfo = AuthenticationUtils
            .AuthenticateAsync(KsefClient, SignatureService, nip)
            .GetAwaiter().GetResult();

        accessToken = authInfo.AccessToken.Token;
        sellerNip = nip;
    }

    /// <summary>
    /// End-to-end test weryfikujący pełny, poprawny przebieg przetwarzania sesji wsadowej w KSeF.
    /// Generuje 20 faktur z szablonu, szyfruje i dzieli paczkę na części, otwiera sesję,
    /// wysyła wszystkie części, zamyka sesję, sprawdza status przetwarzania oraz pobiera UPO
    /// dla pojedynczej faktury i UPO zbiorcze sesji.
    /// </summary>
    /// <remarks>
    /// Kroki:
    /// 1. Przygotowanie paczki (ZIP, szyfrowanie, podział) i otwarcie sesji; zapis numeru referencyjnego.
    /// 2. Wysłanie wszystkich zaszyfrowanych części i krótka pauza.
    /// 3. Zamknięcie sesji i dłuższa pauza na zakończenie przetwarzania.
    /// 4. Weryfikacja statusu sesji: SuccessfulInvoiceCount == 20, FailedInvoiceCount == 0, Status.Code == 200; pobranie numeru referencyjnego UPO.
    /// 5. Pobranie dokumentów sesji i zapis pierwszego numeru KSeF.
    /// 6. Pobranie UPO faktury po numerze KSeF.
    /// 7. Pobranie UPO zbiorczego sesji.
    /// </remarks>
    [Theory]
    [InlineData(SystemCodeEnum.FA2, "invoice-template-fa-2.xml")]
    [InlineData(SystemCodeEnum.FA3, "invoice-template-fa-3.xml")]
    public async Task BatchSession_FullIntegrationFlow_ReturnsUpo(SystemCodeEnum systemCode, string invoiceTemplatePath)
    {
        // 1. Przygotowanie paczki i otwarcie sesji
        OpenBatchSessionResult openResult = await PrepareAndOpenBatchSessionAsync(
            CryptographyService,
            TotalInvoices,
            PartQuantity,
            sellerNip,
            systemCode,
            invoiceTemplatePath,
            accessToken
        );

        // Assercje dla kroku 1
        Assert.NotNull(openResult);
        Assert.False(string.IsNullOrWhiteSpace(openResult.ReferenceNumber));
        Assert.NotNull(openResult.OpenBatchSessionResponse);
        Assert.NotNull(openResult.EncryptedParts);
        Assert.NotEmpty(openResult.EncryptedParts);

        batchSessionReferenceNumber = openResult.ReferenceNumber;
        openBatchSessionResponse = openResult.OpenBatchSessionResponse;
        encryptedParts = openResult.EncryptedParts;

        // 2. Wysłanie wszystkich części
        await SendAllBatchPartsAsync(openBatchSessionResponse, encryptedParts);
        await Task.Delay(2000);

        // 3. Zamknięcie sesji
        Assert.False(string.IsNullOrWhiteSpace(batchSessionReferenceNumber));
        await CloseBatchSessionAsync(batchSessionReferenceNumber!, accessToken);
        await Task.Delay(8000);

        // 4. Status sesji
        SessionStatusResponse statusResponse = await GetBatchSessionStatusAsync(batchSessionReferenceNumber, accessToken);

        Assert.NotNull(statusResponse);
        Assert.True(statusResponse.SuccessfulInvoiceCount == TotalInvoices);
        Assert.Equal(ExpectedFailedInvoiceCount, statusResponse.FailedInvoiceCount);
        Assert.NotNull(statusResponse.Upo);
        Assert.Equal(ExpectedSessionStatusCode, statusResponse.Status.Code);

        upoReferenceNumber = statusResponse.Upo.Pages.First().ReferenceNumber;

        // 5. Dokumenty sesji
        Client.Core.Models.Sessions.SessionInvoicesResponse documents = await GetBatchSessionInvoicesAsync(batchSessionReferenceNumber!, accessToken, 0, TotalInvoices);

        Assert.NotNull(documents);
        Assert.NotEmpty(documents.Invoices);
        Assert.Equal(TotalInvoices, documents.Invoices.Count);

        ksefNumber = documents.Invoices.First().KsefNumber;

        // 6. UPO faktury po numerze KSeF
        string invoiceUpoXml = await GetInvoiceUpoByKsefNumberAsync(
            batchSessionReferenceNumber!,
            ksefNumber!,
            accessToken
        );
        Assert.False(string.IsNullOrWhiteSpace(invoiceUpoXml));
        InvoiceUpo invoiceUpo = UpoUtils.UpoParse<InvoiceUpo>(invoiceUpoXml);
        Assert.Equal(invoiceUpo.Document.KSeFDocumentNumber, ksefNumber);



        // 7. UPO zbiorcze sesji
        string sessionUpoXml = await GetSessionUpoAsync(
            batchSessionReferenceNumber!,
            upoReferenceNumber!,
            accessToken
        );

        Assert.False(string.IsNullOrWhiteSpace(sessionUpoXml));
        SessionUpo sessionUpo = UpoUtils.UpoParse<SessionUpo>(sessionUpoXml);
        Assert.Equal(sessionUpo.ReferenceNumber, openResult.OpenBatchSessionResponse.ReferenceNumber);
    }

    /// <summary>
    /// Generuje faktury z szablonu (Templates/invoice-template-fa-{x}.xml), buduje ZIP, szyfruje i dzieli paczkę na części
    /// Zwraca numer referencyjny sesji, odpowiedź otwarcia sesji i listę zaszyfrowanych części.
    /// </summary>
    private async Task<OpenBatchSessionResult> PrepareAndOpenBatchSessionAsync(
        ICryptographyService cryptographyService,
        int invoiceCount,
        int partQuantity,
        string sellerNip,
        SystemCodeEnum systemCode,
        string invoiceTemplatePath,
        string accessToken)
    {
        Client.Core.Models.Sessions.EncryptionData encryptionData = cryptographyService.GetEncryptionData();

        List<(string FileName, byte[] Content)> invoices = BatchUtils.GenerateInvoicesInMemory(
            count: invoiceCount,
            nip: sellerNip,
            templatePath: invoiceTemplatePath);

        (byte[] zipBytes, Client.Core.Models.Sessions.FileMetadata zipMeta) =
            BatchUtils.BuildZip(invoices, cryptographyService);

        List<Client.Core.Models.Sessions.BatchSession.BatchPartSendingInfo> encryptedParts =
            BatchUtils.EncryptAndSplit(zipBytes, encryptionData, cryptographyService, partQuantity);

        Client.Core.Models.Sessions.BatchSession.OpenBatchSessionRequest openBatchRequest =
            BatchUtils.BuildOpenBatchRequest(zipMeta, encryptionData, encryptedParts, systemCode);

        Client.Core.Models.Sessions.BatchSession.OpenBatchSessionResponse openBatchSessionResponse =
            await BatchUtils.OpenBatchAsync(KsefClient, openBatchRequest, accessToken);

        return new OpenBatchSessionResult(
            openBatchSessionResponse.ReferenceNumber,
            openBatchSessionResponse,
            encryptedParts
        );
    }

    /// <summary>
    /// Wysyła wszystkie zaszyfrowane części paczki dla wcześniej otwartej sesji wsadowej.
    /// </summary>
    private async Task SendAllBatchPartsAsync(
        Client.Core.Models.Sessions.BatchSession.OpenBatchSessionResponse openBatchSessionResponse,
        List<Client.Core.Models.Sessions.BatchSession.BatchPartSendingInfo> encryptedParts)
    {
        await KsefClient.SendBatchPartsAsync(openBatchSessionResponse, encryptedParts);
    }

    /// <summary>
    /// Zamyka sesję wsadową na podstawie numeru referencyjnego.
    /// </summary>
    private async Task CloseBatchSessionAsync(string sessionReferenceNumber, string accessToken)
    {
        await BatchUtils.CloseBatchAsync(KsefClient, sessionReferenceNumber, accessToken);
    }

    /// <summary>
    /// Pobiera status sesji wsadowej.
    /// </summary>
    private async Task<SessionStatusResponse> GetBatchSessionStatusAsync(
        string sessionReferenceNumber,
        string accessToken)
    {
        return await KsefClient.GetSessionStatusAsync(sessionReferenceNumber, accessToken);
    }

    /// <summary>
    /// Pobiera dokumenty (faktury) sesji wsadowej z obsługą parametrów stronicowania.
    /// </summary>
    private async Task<Client.Core.Models.Sessions.SessionInvoicesResponse> GetBatchSessionInvoicesAsync(
        string sessionReferenceNumber,
        string accessToken,
        int offset,
        int count)
    {
        return await BatchUtils.GetSessionInvoicesAsync(KsefClient, sessionReferenceNumber, accessToken, offset, count);
    }

    /// <summary>
    /// Pobiera UPO pojedynczej faktury z sesji na podstawie jej numeru KSeF.
    /// </summary>
    private async Task<string> GetInvoiceUpoByKsefNumberAsync(
        string sessionReferenceNumber,
        string ksefNumber,
        string accessToken)
    {
        return await BatchUtils.GetSessionInvoiceUpoByKsefNumberAsync(
            KsefClient, sessionReferenceNumber, ksefNumber, accessToken);
    }

    /// <summary>
    /// Pobiera zbiorcze UPO sesji na podstawie numeru referencyjnego UPO.
    /// </summary>
    private async Task<string> GetSessionUpoAsync(
        string sessionReferenceNumber,
        string upoReferenceNumber,
        string accessToken)
    {
        string upoResponse = await KsefClient.GetSessionUpoAsync(sessionReferenceNumber, upoReferenceNumber, accessToken, CancellationToken);
        return upoResponse;
    }
}