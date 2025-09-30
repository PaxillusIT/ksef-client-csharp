using KSeF.Client.Core.Exceptions;
using KSeF.Client.Core.Models.Authorization;
using KSeF.Client.Core.Models.Invoices;
using KSeF.Client.Core.Models.Sessions;
using KSeF.Client.Core.Models.Sessions.OnlineSession;
using KSeF.Client.Tests.Utils;
using KSeF.Client.Core.Models.Sessions;

namespace KSeF.Client.Tests.Core.E2E.Invoice;

[Collection("InvoicesScenario")]
public class InvoiceE2ETests : TestBase
{
    private const int PageOffset = 0;
    private const int PageSize = 10;
    private const int DateRangeDays = 30;
    private readonly string SellerNip = MiscellaneousUtils.GetRandomNip();
    private readonly string AccessToken;

    /// <summary>
    /// Konstruktor testów E2E dla faktur. Ustawia token dostępu na podstawie uwierzytelnienia.
    /// </summary>
    public InvoiceE2ETests()
    {
        Client.Core.Models.Authorization.AuthOperationStatusResponse authOperationStatusResponse =
            AuthenticationUtils.AuthenticateAsync(KsefClient, SignatureService, SellerNip).GetAwaiter().GetResult();
        AccessToken = authOperationStatusResponse.AccessToken.Token;
    }

    /// <summary>
    /// Pobiera metadane faktury na podstawie zapytania i sprawdza czy odpowiedź nie jest pusta.
    /// </summary>
    [Fact]
    public async Task Invoice_GetInvoiceMetadataAsync_ReturnsMetadata()
    {
        // Arrange
        InvoiceQueryFilters invoiceMetadataQueryRequest = new InvoiceQueryFilters
        {
            SubjectType = SubjectType.Subject1,
            DateRange = new DateRange
            {
                From = DateTime.UtcNow.AddDays(-DateRangeDays),
                To = DateTime.UtcNow,
                DateType = DateType.Issue
            }
        };

        // Act
        PagedInvoiceResponse metadata = await KsefClient.QueryInvoiceMetadataAsync(
            requestPayload: invoiceMetadataQueryRequest,
            accessToken: AccessToken,
            cancellationToken: CancellationToken,
            pageOffset: PageOffset,
            pageSize: PageSize);

        // Assert
        Assert.NotNull(metadata);
    }

    [Theory]
    [InlineData(SystemCodeEnum.FA3, "invoice-template-fa-3.xml")]
    public async Task Invoice_GetInvoiceAsync_ReturnsInvoiceXml(SystemCodeEnum systemCode, string invoiceTemplatePath)
    {
        EncryptionData encryptionData = CryptographyService.GetEncryptionData();

        // 1. Rozpocznij sesję online
        OpenOnlineSessionResponse openSessionResponse = await OnlineSessionUtils.OpenOnlineSessionAsync(KsefClient, encryptionData, AccessToken);

        // 2. Wyślij fakturę
        SendInvoiceResponse sendInvoiceResponse = await OnlineSessionUtils.SendInvoiceAsync(KsefClient,
            openSessionResponse.ReferenceNumber,
            AccessToken,
            SellerNip,
            invoiceTemplatePath,
            encryptionData,
            CryptographyService);

        // 3. Sprawdź status faktury
        SessionStatusResponse sendInvoiceStatus = await OnlineSessionUtils.GetOnlineSessionStatusAsync(KsefClient,
            openSessionResponse.ReferenceNumber,
            AccessToken);
        await Task.Delay(SleepTime);


        if (sendInvoiceStatus.SuccessfulInvoiceCount > 0)
        {
            // 4. Zamknij sesję
            await OnlineSessionUtils.CloseOnlineSessionAsync(KsefClient,
                openSessionResponse.ReferenceNumber,
                AccessToken);
        }
        await Task.Delay(SleepTime);

        // 5. Pobierz metadane faktur wysłanych w trakcie sesji
        SessionInvoicesResponse invoicesMetadata = await OnlineSessionUtils.GetSessionInvoicesMetadataAsync(KsefClient,
            openSessionResponse.ReferenceNumber,
            AccessToken);
        //await Task.Delay(SleepTime * 10);

        // 6. Pobierz numer pierwszej faktury z listy metadanych
        string ksefInvoiceNumber = invoicesMetadata.Invoices.FirstOrDefault()?.KsefNumber;

        // 7. Pobierz fakturę po jej numerze KSeF - dostępne tylko dla wystawcy faktury (sprzedawcy)
        string invoice = string.Empty;
        int tryCount = 10;
        bool isSuccessfullTry = false;
        do
        {
            try
            {
                invoice = await KsefClient.GetInvoiceAsync(ksefInvoiceNumber, AccessToken);
                isSuccessfullTry = true;
            }
            catch (KsefApiException e)
            {
                tryCount--;
                await Task.Delay(SleepTime);
            }
            catch (Exception e)
            {
                tryCount--;
                await Task.Delay(SleepTime);
            }
            await Task.Delay(SleepTime);
        } while (!isSuccessfullTry && tryCount > 0);
        Assert.True(!string.IsNullOrWhiteSpace(invoice));

        await Task.Delay(SleepTime * 3);
        // 8. Zaloguj się jako nabywca 
        string buyerNip = MiscellaneousUtils.GetRandomNip();
        AuthOperationStatusResponse buyerAuthInfo = await AuthenticationUtils.AuthenticateAsync(KsefClient,
            SignatureService,
            buyerNip);

        InvoiceQueryFilters query = new InvoiceQueryFilters
        {
            DateRange = new DateRange
            {
                From = DateTime.Now.AddDays(-1),
                To = DateTime.Now.AddDays(1),
                DateType = DateType.Issue
            },
            SubjectType = SubjectType.Subject1
        };

        // 9. Jako nabywca pobierz metadane faktur
        PagedInvoiceResponse invoicesMetadataForBuyer = await KsefClient.QueryInvoiceMetadataAsync(query, buyerAuthInfo.AccessToken.Token);
        Assert.NotNull(invoicesMetadataForBuyer);

        InvoiceExportRequest invoiceExportRequest = new InvoiceExportRequest
        {
            Encryption = encryptionData.EncryptionInfo,
            Filters = query
        };

        await Task.Delay(SleepTime * 10);
        ExportInvoicesResponse invoicesForBuyerResponse = await KsefClient.ExportInvoicesAsync(invoiceExportRequest,
        buyerAuthInfo.AccessToken.Token);

        await Task.Delay(SleepTime * 10);

        InvoiceExportStatusResponse exportStatus = await KsefClient.GetInvoiceExportStatusAsync(invoicesForBuyerResponse.OperationReferenceNumber,
            buyerAuthInfo.AccessToken.Token);
        await Task.Delay(SleepTime);

        Assert.NotNull(exportStatus);
    }
}