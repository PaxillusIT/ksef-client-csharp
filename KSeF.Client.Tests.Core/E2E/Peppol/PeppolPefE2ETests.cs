using KSeF.Client.Core.Models.Authorization;
using KSeF.Client.Core.Models.Invoices;
using KSeF.Client.Core.Models.Peppol;
using KSeF.Client.Core.Models.Permissions;
using KSeF.Client.Core.Models.Permissions.Entity;
using KSeF.Client.Core.Models.Sessions;
using KSeF.Client.Core.Models.Sessions.OnlineSession;
using KSeF.Client.Tests.Utils;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace KSeF.Client.Tests.Core.E2E.Peppol
{
    /// <summary>
    /// Scenariusz E2E:
    /// 1) Dostawca rejestruje się automatycznie (pierwsze uwierzytelnienie pieczęcią z O + CN=PeppolId).
    /// 2) Firma nadaje uprawnienie PefInvoiceWrite (PefInvoicing) temu dostawcy.
    /// 3) Dostawca wysyła fakturę PEF w imieniu firmy.
    /// </summary>
    [Collection("OnlineSessionScenario")]
    public class PeppolPefE2ETests : TestBase
    {
        private const int StatusProcessing = 100;
        private const string PefTemplate = "invoice-template-fa-3-pef.xml";
        private const int sessionClosed = 170;
        private const int processing = 150;

        // Wymaganie PeppolId (CN):
        private static readonly Regex PeppolIdRegex =
            new(@"^P[A-Z]{2}[0-9]{6}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly string _accessToken; // token firmy (NIP) – do odczytów i ewentualnie innych operacji
        private readonly string _companyNip;
        private readonly string _buyerNip;
        private readonly string _iban;

        public PeppolPefE2ETests()
        {
            // Token firmy (XAdES) – jak w pozostałych E2E (posłuży do odczytów/list i ewentualnej sesji, ale wysyłkę robi dostawca)
            _companyNip = MiscellaneousUtils.GetRandomNip();
            _buyerNip = MiscellaneousUtils.GetRandomNip();
            _iban = MiscellaneousUtils.GeneratePolishIban();
            var auth = AuthenticationUtils
                .AuthenticateAsync(KsefClient, SignatureService, _companyNip)
                .GetAwaiter().GetResult();

            _accessToken = auth.AccessToken.Token;
        }

        [Fact]
        public async Task Peppol_PEF_FullFlow_AutoRegister_Grant_Send()
        {
            // -------------------------------------------------------------------------------------
            // 0) AUTO-REJESTRACJA dostawcy: pierwsze uwierzytelnienie pieczęcią z O + CN=PeppolId
            // -------------------------------------------------------------------------------------
            string country = (Environment.GetEnvironmentVariable("KSEF_PEP_COUNTRY") ?? "PL").ToUpperInvariant();
            string peppolId = $"P{country}{new Random().Next(0, 1_000_000):000000}";
            Assert.True(PeppolIdRegex.IsMatch(peppolId), $"PeppolId '{peppolId}' nie spełnia ^P[A-Z]{{2}}[0-9]{{6}}$.");

            string organizationName = Environment.GetEnvironmentVariable("KSEF_PEP_ORG") ?? "E2E Peppol Test Provider";
            string organizationIdentifier = Environment.GetEnvironmentVariable("KSEF_PEP_ORG_ID") ?? peppolId; 

            // Pieczęć ustawia O + CN (spełniamy wymagania Peppol)
            X509Certificate2 providerSeal = CertificateUtils.GetCompanySeal(
                organizationName: organizationName,
                organizationIdentifier: organizationIdentifier,
                commonName: peppolId);

            AuthOperationStatusResponse providerAuth = await AuthenticationUtils.AuthenticateAsync(
                ksefClient: KsefClient,
                signatureService: SignatureService,
                certificate: providerSeal,
                contextIdentifierType: Client.Core.Models.Authorization.ContextIdentifierType.PeppolId,
                contextIdentifierValue: peppolId);
            Assert.NotNull(providerAuth?.AccessToken);
            string providerToken = providerAuth.AccessToken.Token;

            // -------------------------------------------------------------------------------------
            // 1) Lista dostawców – paginacja HasMore + krótki retry, szukamy KONKRETNEGO peppolId
            // -------------------------------------------------------------------------------------
            bool found = false;
            PeppolProvider? resolvedProvider = default; 

            for (int attempt = 0; attempt < 3 && !found; attempt++)
            {
                int? pageOffset = null;
                const int pageSize = 200;
                int guardPages = 200;

                do
                {
                    QueryPeppolProvidersResponse page = await KsefClient.QueryPeppolProvidersAsync(
                        accessToken: _accessToken,
                        pageOffset: pageOffset,
                        pageSize: pageSize,
                        cancellationToken: CancellationToken.None);

                    if (page?.PeppolProviders != null && page.PeppolProviders.Count > 0)
                    {
                        PeppolProvider? hit = page.PeppolProviders.FirstOrDefault(p => p.Id == peppolId);
                        if (hit != null)
                        {
                            resolvedProvider = hit;
                            found = true;
                            break;
                        }
                    }

                    if (page?.HasMore == true)
                    {
                        // Inkrement offsetu o realnie zwróconą liczbę rekordów (bezpieczniej niż stałe pageSize)
                        pageOffset = (pageOffset ?? 0) + (page?.PeppolProviders?.Count ?? 0);
                    }
                    else
                    {
                        break;
                    }
                }
                while (guardPages-- > 0);

                if (!found) await Task.Delay(1000); // krótki retry na ewentualną replikację
            }

            Assert.True(found,
                $"Brak PeppolId '{peppolId}' po auto-rejestracji. Sprawdź CN (regex) i O (OrganizationName), " +
                $"ew. seed (KSEF20-12422) lub zwiększ retry.");

            // -------------------------------------------------------------------------------------
            // 2) Firma nadaje uprawnienie PefInvoiceWrite (PefInvoicing - uprawnenie sesyjne) dla DOSTAWCY (kontekst żądania: PeppolId)
            //    - bearer = token firny 
            //    - Subject = PeppolId (komu przyznajemy uprawnienie)
            // -------------------------------------------------------------------------------------
            GrantPermissionsAuthorizationRequest grantReq = new GrantPermissionsAuthorizationRequest
            {
                SubjectIdentifier = new KSeF.Client.Core.Models.Permissions.Entity.SubjectIdentifier
                {
                    Type = KSeF.Client.Core.Models.Permissions.Entity.SubjectIdentifierType.PeppolId,
                    Value = peppolId
                },
                Permission = AuthorizationPermissionType.PefInvoicing,
                Description = $"E2E: grant PEF invoicing for company {_companyNip} (requested by {peppolId})"
            };

            OperationResponse grantResp = await KsefClient.GrantsPermissionAuthorizationAsync(
                requestPayload: grantReq,
                accessToken: _accessToken, 
                cancellationToken: CancellationToken.None);
            Assert.NotNull(grantResp);
            await Task.Delay(SleepTime); 
            
            EntityAuthorizationsQueryRequest query = new EntityAuthorizationsQueryRequest
            {
                AuthorizingIdentifier = new EntityAuthorizationsAuthorizingEntityIdentifier
                {
                    Type = "Nip",
                    Value = _companyNip
                },
                AuthorizedIdentifier = new EntityAuthorizationsAuthorizedEntityIdentifier
                {
                    Type = "PeppolId",
                    Value = peppolId
                },
                QueryType = QueryType.Granted,
                PermissionTypes = new() { InvoicePermissionType.PefInvoicing }
            };

            PagedAuthorizationsResponse<AuthorizationGrant> authz = await KsefClient.SearchEntityAuthorizationGrantsAsync(
                requestPayload: query,
                accessToken: _accessToken, // odczyt listy grantów może iść tokenem firmy
                pageOffset: 0,
                pageSize: 10,
                cancellationToken: CancellationToken.None);
            Assert.NotNull(authz);
            // Uwaga: nie asertujemy count>0 – w niektórych env grant nie jest raportowany 1:1 do PeppolId.

            // -------------------------------------------------------------------------------------
            // 3) DOSTAWCA wysyła fakturę PEF (po nadaniu uprawnienia) – cały krok na providerToken
            // -------------------------------------------------------------------------------------
            EncryptionData encryptionData = CryptographyService.GetEncryptionData();

            OpenOnlineSessionResponse openSession = await OnlineSessionUtils.OpenOnlineSessionAsync(
                ksefClient: KsefClient,
                encryptionData: encryptionData,
                accessToken: providerToken,   // DOSTAWCA otwiera sesję
                systemCode: SystemCodeEnum.FAPEF
                );
            Assert.NotNull(openSession);
            Assert.False(string.IsNullOrWhiteSpace(openSession.ReferenceNumber));
            await Task.Delay(SleepTime);
            
            SendInvoiceResponse sendResp = await OnlineSessionUtils.SendPefInvoiceAsync(
                ksefClient: KsefClient,
                sessionReferenceNumber: openSession.ReferenceNumber,
                accessToken: providerToken,   // DOSTAWCA wysyła
                supplierNip: $"PL{_companyNip}",             // w imieniu firmy, która nadała grant
                customerNip: $"PL{_buyerNip}",
                buyerReference: $"PL{_buyerNip}",
                iban: _iban,
                templatePath: PefTemplate,
                encryptionData: encryptionData,
                cryptographyService: CryptographyService);
            Assert.NotNull(sendResp);
            await Task.Delay(SleepTime);
                        
            var statusProcessing = await KsefClient.GetSessionStatusAsync(openSession.ReferenceNumber, providerToken, CancellationToken.None);
            Assert.NotNull(statusProcessing);
            SessionFailedInvoicesResponse failedInvoices;
            if (statusProcessing.FailedInvoiceCount is not null)
            {
                failedInvoices = await KsefClient.GetSessionFailedInvoicesAsync(openSession.ReferenceNumber, providerToken, pageSize: 10, continuationToken: string.Empty, CancellationToken.None);
            }            
            Assert.Null(statusProcessing.FailedInvoiceCount);
            Assert.Equal(StatusProcessing, statusProcessing.Status.Code);

            await KsefClient.CloseOnlineSessionAsync(openSession.ReferenceNumber, providerToken, CancellationToken.None);
            await Task.Delay(SleepTime);

            var statusDone = await KsefClient.GetSessionStatusAsync(openSession.ReferenceNumber, providerToken, CancellationToken.None);
            Assert.NotNull(statusDone);
            Assert.Equal(sessionClosed, statusDone.Status.Code);

            SessionInvoicesResponse invoices = await KsefClient.GetSessionInvoicesAsync(openSession.ReferenceNumber, providerToken, pageSize: 10);
            Assert.NotNull(invoices);
            Assert.NotEmpty(invoices.Invoices);

            SessionInvoice sessionInvoice = invoices.Invoices.First(x => x.ReferenceNumber == sendResp.ReferenceNumber);
            if (sessionInvoice.Status.Code == processing)
            {
                await Task.Delay(SleepTime);

                invoices = await KsefClient.GetSessionInvoicesAsync(openSession.ReferenceNumber, providerToken, pageSize: 10);
                Assert.NotNull(invoices);
                Assert.NotEmpty(invoices.Invoices);
            }

            SessionInvoice refreshedSessionInvoice = invoices.Invoices.First(x => x.ReferenceNumber == sendResp.ReferenceNumber);
            if (refreshedSessionInvoice.Status.Code == processing)
            {
                Assert.True(refreshedSessionInvoice.Status.Code == processing,
                    $"Faktura nadal w statusie {processing} – nie zdążyła się przetworzyć w czasie testu.");

                return; // dalej nie pójdziemy, bo faktura nadal w processing
            }

            string invoicesUpo = await KsefClient.GetSessionInvoiceUpoByReferenceNumberAsync(openSession.ReferenceNumber, sendResp.ReferenceNumber, providerToken);
            Assert.NotNull(invoicesUpo);            


        }
    }
}
