using KSeF.Client.Api.Services;
using KSeF.Client.Core.Models.Invoices;
using KSeF.Client.Core.Models.Sessions;
using KSeF.Client.Core.Models.Sessions.OnlineSession;
using KSeF.Client.DI;
using KSeF.Client.Tests.Utils;
using System.Buffers.Text;

namespace KSeF.Client.Tests;

public class QrCodeOnlineE2EScenarioFixture
{
    public string? AccessToken { get; set; }
    public string? Nip { get; set; }
    public string? SessionReferenceNumber { get; set; }
}

[CollectionDefinition("QrCodeOnlineE2EScenario")]
public class QrCodeOnlineE2EScenarioCollection
: ICollectionFixture<QrCodeOnlineE2EScenarioFixture>
{ }
[Collection("QrCodeOnlineE2EScenario")]
public class QrCodeOnlineE2ETests : KsefIntegrationTestBase
{
    private readonly QrCodeOnlineE2EScenarioFixture _f;
    private readonly EncryptionData _encryptionData;
    private readonly QrCodeService _qrSvc;
    private readonly VerificationLinkService _linkSvc;
    private const int SleepTime = 500;
    private const int SuccessfulSessionStatusCode = 200;
    private const int SessionPendingStatusCode = 170;
    private const int SessionFailedStatusCode = 445;

    public QrCodeOnlineE2ETests(QrCodeOnlineE2EScenarioFixture f)
    {
        _f = f;
        _linkSvc = new VerificationLinkService(new KSeFClientOptions() { BaseUrl = KsefEnviromentsUris.TEST});
        _encryptionData = CryptographyService.GetEncryptionData();
        _qrSvc = new QrCodeService();
        _f.Nip = MiscellaneousUtils.GetRandomNip();

        Core.Models.Authorization.AuthOperationStatusResponse authInfo = AuthenticationUtils.AuthenticateAsync(KsefClient, SignatureService, _f.Nip).GetAwaiter().GetResult();
        _f.AccessToken = authInfo.AccessToken.Token;
    }

    /// <summary>
    /// End-to-end test weryfikujący pełny, zakończony sukcesem przebieg wystawienia kodu QR do faktury w trybie interaktywnym (online session).
    /// Test używa faktury FA(2) oraz szyfrfowania RSA.
    /// </summary>
    /// <remarks>
    /// Kroki:
    /// 1. Autoryzacja, pozyskanie tokenu dostępu. (w konstruktorze)
    /// 2. Otwarcie sesji online z szyfrowaniem RSA.
    /// 3. Utworzenie i wysłanie pojedynczej faktury FA(2).
    /// 4. Weryfikacja, czy faktura została dodana do sesji.
    /// 5. Zamknięcie sesji online.
    /// 6. Sprawdzenie statusu sesji, oczekiwanie na zakończenie przetwarzania faktur.
    /// 7. Sprawdzenie statusu faktury.
    /// 8. Pobranie metadanych faktur z sesji.
    /// 9. Znalezienie metadanych faktury wśród metadanych wszystkich faktur z sesji.
    /// 10. Stworzenie linku weryfikacyjnego do faktury za pomoca certyfikatu oraz hashu faktury.
    /// 11. Utworzenie kodu QR dla trybu online.
    /// 12. Dodanie napisu z numerem faktury do kodu QR (Label).
    /// </remarks>
    [Fact]
    public async Task QrCodeOnlineE2ETest()
    {
        //Otwarcie sesji online z szyfrowaniem RSA. 
        OpenOnlineSessionResponse openSessionResponse = await OnlineSessionUtils.OpenOnlineSessionAsync(
            KsefClient, 
            _encryptionData, 
            _f.AccessToken,
            SystemCodeEnum.FA2);
        Assert.NotNull(openSessionResponse);
        Assert.False(string.IsNullOrWhiteSpace(openSessionResponse.ReferenceNumber));

        _f.SessionReferenceNumber = openSessionResponse.ReferenceNumber;

        //Utworzenie i wysłanie faktury FA(2)
        SendInvoiceResponse sendInvoiceResponse = await OnlineSessionUtils.SendInvoiceAsync(
        KsefClient,
        openSessionResponse.ReferenceNumber,
        _f.AccessToken,
        _f.Nip,
        "invoice-template-fa-2.xml",
        _encryptionData,
        CryptographyService);

        Assert.NotNull(sendInvoiceResponse);
        Assert.False(string.IsNullOrWhiteSpace(sendInvoiceResponse.ReferenceNumber));

        //Weryfikacja, czy faktura została dodana do sesji.
        SessionStatusResponse sessionStatus = await OnlineSessionUtils.GetOnlineSessionStatusAsync(
        KsefClient,
        openSessionResponse.ReferenceNumber,
        _f.AccessToken);

        Assert.NotNull(sessionStatus);
        Assert.True(sessionStatus.Status.Code > 0);
        Assert.NotNull(sessionStatus.InvoiceCount);

        //Zamknięcie sesji online
        await OnlineSessionUtils.CloseOnlineSessionAsync(
            KsefClient,
            openSessionResponse.ReferenceNumber,
            _f.AccessToken);

        //Sprawdzenie statusu sesji, oczekiwanie na zakończenie przetwarzania faktur
        do
        {
            await Task.Delay(SleepTime);
            sessionStatus = await OnlineSessionUtils.GetOnlineSessionStatusAsync(
                KsefClient,
                openSessionResponse.ReferenceNumber,
                _f.AccessToken);
        } while (sessionStatus.Status.Code == SessionPendingStatusCode);

        Assert.NotEqual(SessionFailedStatusCode, sessionStatus.Status.Code);

        //Sprawdzenie statusu faktury
        SessionInvoice invoicesStatus = await KsefClient.GetSessionInvoiceAsync(_f.SessionReferenceNumber,
            sendInvoiceResponse.ReferenceNumber,
            _f.AccessToken);

        Assert.NotNull(invoicesStatus);

        int numbersOfTriesForInvoice = 0;
        while (invoicesStatus.Status.Code == 150 && numbersOfTriesForInvoice < 15)
        {
            await Task.Delay(SleepTime);
            invoicesStatus = await KsefClient.GetSessionInvoiceAsync(_f.SessionReferenceNumber,
                sendInvoiceResponse.ReferenceNumber,
                _f.AccessToken);
            numbersOfTriesForInvoice++;
        }

        Assert.Equal(SuccessfulSessionStatusCode, invoicesStatus.Status.Code);

        //Pobranie metadanych faktur z sesji
        SessionInvoicesResponse invoicesMetadata = await OnlineSessionUtils.GetSessionInvoicesMetadataAsync(
            KsefClient,
            openSessionResponse.ReferenceNumber,
            _f.AccessToken);

        Assert.NotNull(invoicesMetadata);
        Assert.NotEmpty(invoicesMetadata.Invoices);

        //Znalezienie metadanych faktury wśród metadanych wszystkich faktur z sesji
        SessionInvoice invoiceMetadata = invoicesMetadata.Invoices.Single(x => x.ReferenceNumber == sendInvoiceResponse.ReferenceNumber);
        string invoiceKsefNumber = invoiceMetadata.KsefNumber;
        string invoiceHash = invoiceMetadata.InvoiceHash;
        DateTimeOffset invoicingDate = invoiceMetadata.InvoicingDate;

        //Stworzenie linku weryfikacyjnego do faktury za pomoca certyfikatu oraz hashu faktury 
        string invoiceForOnlineUrl = _linkSvc.BuildInvoiceVerificationUrl(_f.Nip, invoicingDate.DateTime, invoiceHash);

        Assert.NotNull(invoiceForOnlineUrl);
        Assert.Contains(Base64Url.EncodeToString(Convert.FromBase64String(invoiceHash)), invoiceForOnlineUrl);
        Assert.Contains(_f.Nip, invoiceForOnlineUrl);
        Assert.Contains(invoicingDate.ToString("dd-MM-yyyy"), invoiceForOnlineUrl);

        //Utworzenie kodu QR dla trybu online
        byte[] qrOnline = _qrSvc.GenerateQrCode(invoiceForOnlineUrl);

        Assert.NotNull(qrOnline);

        //Dodanie napisu z numerem faktury do kodu QR (Label)
        qrOnline = _qrSvc.AddLabelToQrCode(qrOnline, invoiceKsefNumber);

        Assert.NotEmpty(qrOnline);
    }
}