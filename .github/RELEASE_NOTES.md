> Info: 🔧 zmienione • ➕ dodane • ➖ usunięte • 🔀 przeniesione

---
# Changelog zmian – ## Wersja 2.0.0 RC5.1
---

### Nowe
- **Tests**
  - Obsługa `KsefApiException` (np. 403 *Forbidden*) w scenariuszach sesji i E2E.

### Zmodyfikowane
- **Invoices / Export**
  - `ExportInvoicesResponse` – usunięto pole `Status`; po `ExportInvoicesAsync` używaj `GetInvoiceExportStatusAsync(operationReferenceNumber)`.
- **Invoices / Metadata**
  - `pageSize` – zakres dozwolony **10–250** (zaktualizowane testy: „outside 10–250”).
- **Tests (E2E)**
  - Pobieranie faktury: retry **5 → 10**, precyzyjny `catch` dla `KsefApiException`, asercje `IsNullOrWhiteSpace`.
- **Utils**
  - `OnlineSessionUtils` – prefiks **`PL`** dla `supplierNip` i `customerNip`.
- **Peppol tests**
  - Zmieniono użycie NIP na format z prefiksem `PL...`.
  - Dodano asercję w testach PEF, jeśli faktura pozostaje w statusie *processing*.
- **Permissions**
  - Dostosowanie modeli i testów do nowego kontraktu API.
### Usunięte
- **Invoices / Export**
  - `ExportInvoicesResponse.Status`.

### Poprawki i zmiany dokumentacji
- Przykłady eksportu bez `Status`.
- Opis wyjątków (`KsefApiException`, 403 *Forbidden*).
- Limit `pageSize` zaktualizowany do **10–250**.

---
# Changelog zmian – ### Wersja 2.0.0 RC5
---

### Nowe
- **Auth**
  - `ContextIdentifierType` → dodano wartość `PeppolId`
  - `AuthenticationMethod` → dodano wartość `PeppolSignature`
  - `AuthTokenRequest` → nowe property `AuthorizationPolicy`
  - `AuthorizationPolicy` → nowy model zastępujący `IpAddressPolicy`
  - `AllowedIps` → nowy model z listami `Ip4Address`, `Ip4Range`, `Ip4Mask`
  - `AuthTokenRequestBuilder` → nowa metoda `WithAuthorizationPolicy(...)`
  - `ContextIdentifierType` → dodano wartość `PeppolId`
- **Models**
  - `StatusInfo` → dodano property `StartDate`, `AuthenticationMethod`
  - `AuthorizedSubject` → nowy model (`Nip`, `Name`, `Role`)
  - `ThirdSubjects` → nowy model (`IdentifierType`, `Identifier`, `Name`, `Role`)
  - `InvoiceSummary` → dodano property `HashOfCorrectedInvoice`, `AuthorizedSubject`, `ThirdSubjects`
  - `AuthenticationKsefToken` → dodano property `LastUseDate`, `StatusDetails`
  - `InvoiceExportRequest`, `ExportInvoicesResponse`, `InvoiceExportStatusResponse`, `InvoicePackage` → nowe modele eksportu faktur (zastępują poprzednie)
  - `FormType` → nowy enum (`FA`, `PEF`, `RR`) używany w `InvoiceQueryFilters`
  - `OpenOnlineSessionResponse`
      - dodano property `ValidUntil : DateTimeOffset`
      - zmiana modelu requesta w dokumentacji endpointu `QueryInvoiceMetadataAsync` (z `QueryInvoiceRequest` na `InvoiceMetadataQueryRequest`)
      - zmiana namespace z `KSeFClient` na `KSeF.Client`
- **Enums**
  - `InvoicePermissionType` → dodano wartości `RRInvoicing`, `PefInvoicing`
  - `AuthorizationPermissionType` → dodano wartość `PefInvoicing`
  - `KsefTokenPermissionType` → dodano wartości `SubunitManage`, `EnforcementOperations`, `PeppolId`
  - `ContextIdentifierType (Tokens)` → nowy enum (`Nip`, `Pesel`, `Fingerprint`)
  - `PersonPermissionsTargetIdentifierType` → dodano wartość `AllPartners`
  - `SubjectIdentifierType` → dodano wartość `PeppolId`
- **Interfaces**
  - `IKSeFClient` → nowe metody:
    - `ExportInvoicesAsync` – `POST /api/v2/invoices/exports`
    - `GetInvoiceExportStatusAsync` – `GET /api/v2/invoices/exports/{operationReferenceNumber}`
    - `GetAttachmentPermissionStatusAsync` – poprawiony na `GET /api/v2/permissions/attachments/status`
    - `SearchGrantedPersonalPermissionsAsync` – `POST /api/v2/permissions/query/personal/grants`
    - `GrantsPermissionAuthorizationAsync` – `POST /api/v2/permissions/authorizations/grants`
    - `QueryPeppolProvidersAsync` – `GET /api/v2/peppol/query`
- **Tests**
  - `Authenticate.feature.cs` → dodano testy end-to-end dla procesu uwierzytelniania.

### Zmodyfikowane
- **authv2.xsd**
  - ➖ Usunięto:
    - element `OnClientIpChange (tns:IpChangePolicyEnum)`
    - regułę unikalności `oneIp`
    - cały model `IpAddressPolicy` (`IpAddress`, `IpRange`, `IpMask`)
  - Dodano:
    - element `AuthorizationPolicy` (zamiast `IpAddressPolicy`)
    - nowy model `AllowedIps` z kolekcjami:
      - `Ip4Address` – pattern z walidacją zakresów IPv4 (0–255)
      - `Ip4Range` – rozszerzony pattern z walidacją zakresu adresów
      - `Ip4Mask` – rozszerzony pattern z walidacją maski (`/8`, `/16`, `/24`, `/32`)
  - Zmieniono:
    - `minOccurs/maxOccurs` dla `Ip4Address`, `Ip4Range`, `Ip4Mask`:  
      wcześniej `minOccurs="0" maxOccurs="unbounded"` → teraz `minOccurs="0" maxOccurs="10"`
  - Podsumowanie:
    - Zmieniono nazwę `IpAddressPolicy` → `AuthorizationPolicy`
    - Wprowadzono precyzyjniejsze regexy dla IPv4
    - Ograniczono maksymalną liczbę wpisów do 10
- **Invoices**
  - `InvoiceMetadataQueryRequest` → usunięto `SchemaType`
  - `PagedInvoiceResponse` → `TotalCount` opcjonalny
  - `Seller.Identifier` → opcjonalny, dodano `Seller.Nip` jako wymagane
  - `AuthorizedSubject.Identifier` → usunięty, dodano `AuthorizedSubject.Nip`
  - `fileHash` → usunięty
  - `invoiceHash` → dodany
  - `invoiceType` → teraz `InvoiceType` zamiast `InvoiceMetadataInvoiceType`
  - `InvoiceQueryFilters` → `InvoicingMode` stał się opcjonalny (`InvoicingMode?`), dodano `FormType`, usunięto `IsHidden`
  - `SystemCodes.cs` → dodano kody systemowe dla PEF oraz zaktualizowano mapowanie pod `FormType.PEF`
- **Permissions**
  - `EuEntityAdministrationPermissionsGrantRequest` → dodano wymagane `SubjectName`
  - `ProxyEntityPermissions` → uspójniono nazewnictwo poprzez zmianę na `AuthorizationPermissions`
- **Tokens**
  - `QueryKsefTokensAsync` → dodano parametry `authorIdentifier`, `authorIdentifierType`, `description`; usunięto domyślną wartość `pageSize=10`
  - poprawiono generowanie query string: `status` powtarzany zamiast listy `statuses`

### Poprawki i zmiany dokumentacji
- poprawiono i uzupełniono opisy działania metod w interfejsach `IAuthCoordinator` oraz `ISignatureService`
  - w implementacjach zastosowano `<inheritdoc />` dla spójności dokumentacji

### Zmiany kryptografii
- dodano obsługę ECDSA przy generowaniu CSR (domyślnie algorytm IEEE P1363, możliwość nadpisania na RFC 3279 DER)
- zmieniono padding RSA z PKCS#1 na PSS zgodnie ze specyfikacją KSeF API w implementacji `SignatureService`

### Usunięte
- **Invoices**
  - `AsyncQueryInvoicesAsync` i `GetAsyncQueryInvoicesStatusAsync` → zastąpione przez metody eksportu
  - `AsyncQueryInvoiceRequest`, `AsyncQueryInvoiceStatusResponse` → usunięte
  - `InvoicesExportRequest` → zastąpione przez `InvoiceExportRequest`
  - `InvoicesExportPackage` → zastąpione przez `InvoicePackage`
  - `InvoicesMetadataQueryRequest` → zastąpione przez `InvoiceQueryFilters`
  - `InvoiceExportFilters` → włączone do `InvoiceQueryFilters`





---
# Changelog zmian – ### Wersja 2.0.0 RC4

---

## 1. KSeF.Client
  - Usunięto `Page` i `PageSize` i dodano `HasMore` w: 
    - `PagedInvoiceResponse`
    - `PagedPermissionsResponse<TPermission>`
    - `PagedAuthorizationsResponse<TAuthorization>`
    - `PagedRolesResponse<TRole>`
    - `SessionInvoicesResponse`
   - Usunięto `InternalId` z wartości enum `TargetIdentifierType` w `GrantPermissionsIndirectEntityRequest`
   - Zmieniono odpowiedź z `SessionInvoicesResponse` na nową `SessionFailedInvoicesResponse` w odpowiedzi endpointu `/sessions/{referenceNumber}/invoices/failed`, metoda `GetSessionFailedInvoicesAsync`.
   - Zmieniono na opcjonalne pole `to` w `InvoiceMetadataQueryRequest`, `InvoiceQueryDateRange`, `InvoicesAsyncQueryRequest`.
   - Zmieniono `AuthenticationOperationStatusResponse` na nową `AuthenticationListItem` w `AuthenticationListResponse` w odpowiedzi enpointu `/auth/sessions`.
   - Zmieniono model `InvoiceMetadataQueryRequest` adekwatnie do kontraktu API.
   - Dodano pole `CertificateType` w `SendCertificateEnrollmentRequest`, `CertificateResponse`, `CertificateMetadataListResponse` oraz `CertificateMetadataListRequest`.
   - Dodano `WithCertificateType` w `GetCertificateMetadataListRequestBuilder` oraz `SendCertificateEnrollmentRequestBuilder`.
   - Dodano brakujące pole `ValidUntil` w modelu `Session`.
   - Zmieniono `ReceiveDate` na `InvoicingDate` w modelu `SessionInvoice`.

   
## 2. KSeF.DemoWebApp/Controllers
- **OnlineSessionController.cs**: ➕ `GET /send-invoice-correction` - Przykład implementacji i użycia korekty technicznej
---

```
```

# Changelog zmian – `## 2.0.0 (2025-07-14)` (KSeF.Client)

---

## 1. KSeF.Client
Zmiana wersji .NET 8.0 na .NET 9/0

### 1.1 Api/Services
- **AuthCoordinator.cs**: 🔧 Dodano dodatkowy log `Status.Details`; 🔧 dodano wyjątek przy `Status.Code == 400`; ➖ usunięto `ipAddressPolicy`
- **CryptographyService.cs**: ➕ inicjalizacja certyfikatów; ➕ pola `symetricKeyEncryptionPem`, `ksefTokenPem`
- **SignatureService.cs**: 🔧 `Sign(...)` → `SignAsync(...)`
- **QrCodeService.cs**: ➕ nowa usługa do generowania QrCodes
- **VerificationLinkService.cs**: ➕ nowa usługa generowania linków do weryfikacji faktury

### 1.2 Api/Builders
- **SendCertificateEnrollmentRequestBuilder.cs**: 🔧 `ValidFrom` pole zmienione na opcjonalne ; ➖ interfejs `WithValidFrom`
- **OpenBatchSessionRequestBuilder.cs**: 🔧 `WithBatchFile(...)` usunięto parametr `offlineMode`; ➕ `WithOfflineMode(bool)` nopwy opcjonalny krok do oznaczenia trybu offline

### 1.3 Core/Models
- **StatusInfo.cs**: 🔧 dodano property `Details`; ➖ `BasicStatusInfo` - usunięto klase w c elu unifikacji statusów
- **PemCertificateInfo.cs**: ➕ `PublicKeyPem` - dodano nowe property
- **DateType.cs**: ➕ `Invoicing`, `Acquisition`, `Hidden` - dodano nowe emumeratory do filtrowania faktur
- **PersonPermission.cs**: 🔧 `PermissionScope` zmieniono z PermissionType zgodnie ze zmianą w kontrakcie
- **PersonPermissionsQueryRequest.cs**: 🔧 `QueryType` - dodano nowe wymagane property do filtrowania w zadanym kontekście
- **SessionInvoice.cs**: 🔧 `InvoiceFileName` - dodano nowe property 
- **ActiveSessionsResponse.cs** / `Status.cs` / `Item.cs` (Sessions): ➕ nowe modele

### 1.4 Core/Interfaces
- **IKSeFClient.cs**: 🔧 `GetAuthStatusAsync` → zmiana modelu zwracanego z `BasicStatusInfo` na `StatusInfo` 
➕ Dodano metodę GetActiveSessions(accessToken, pageSize, continuationToken, cancellationToken)
➕ Dodano metodę RevokeCurrentSessionAsync(token, cancellationToken)
➕ Dodano metodę RevokeSessionAsync(referenceNumber, accessToken, cancellationToken)
- **ISignatureService.cs**: 🔧 `Sign` → `SignAsync`
- **IQrCodeService.cs**: nowy interfejs do generowania QRcodes 
- **IVerificationLinkService.cs**: ➕ nowy interfejs do tworzenia linków weryfikacyjnych do faktury

### 1.5 DI & Dependencies
- **ServiceCollectionExtensions.cs**: ➕ rejestracja `IQrCodeService`, `IVerificationLinkService`
- **ServiceCollectionExtensions.cs**: ➕ dodano obsługę nowej właściwości `WebProxy` z `KSeFClientOptions`
- **KSeFClientOptions.cs**: 🔧 walidacja `BaseUrl`
- **KSeFClientOptions.cs**: ➕ dodano właściwości `WebProxy` typu `IWebProxy`
➕ Dodano CustomHeaders - umożliwia dodawanie dodatkowych nagłówków do klienta Http
- **KSeF.Client.csproj**: ➕ `QRCoder`, `System.Drawing.Common`

### 1.6 Http
- **KSeFClient.cs**: ➕ nagłówki `X-KSeF-Session-Id`, `X-Environment`; ➕ `Content-Type: application/octet-stream`

### 1.7 RestClient
- **RestClient.cs**: 🔧 `Uproszczona implementacja IRestClient'

### 1.8 Usunięto
- **KSeFClient.csproj.cs**: ➖ `KSeFClient` - nadmiarowy plik projektu, który był nieużywany
---

## 2. KSeF.Client.Tests
**Nowe pliki**: `QrCodeTests.cs`, `VerificationLinkServiceTests.cs`  
Wspólne: 🔧 `Thread.Sleep` → `Task.Delay`; ➕ `ExpectedPermissionsAfterRevoke`; 4-krokowy flow; obsługa 400  
Wybrane: **Authorization.cs**, `EntityPermission*.cs`, **OnlineSession.cs**, **TestBase.cs**
---

## 3. KSeF.DemoWebApp/Controllers
- **QrCodeController.cs**: ➕ `GET /qr/certificate` ➕`/qr/invoice/ksef` ➕`qr/invoice/offline`
- **ActiveSessionsController.cs**: ➕ `GET /sessions/active`
- **AuthController.cs**: ➕ `GET /auth-with-ksef-certificate`; 🔧 fallback `contextIdentifier`
- **BatchSessionController.cs**: ➕ `WithOfflineMode(false)`; 🔧 pętla `var`
- **CertificateController.cs**: ➕ `serialNumber`, `name`; ➕ builder
- **OnlineSessionController.cs**: ➕ `WithOfflineMode(false)` 🔧 `WithInvoiceHash`

---

## 4. Podsumowanie

| Typ zmiany | Liczba plików |
|------------|---------------|
| ➕ dodane   | 12 |
| 🔧 zmienione| 33 |
| ➖ usunięte | 3 |

---

## [next-version] – `2025-07-15`

### 1. KSeF.Client

#### 1.1 Api/Services
- **CryptographyService.cs**  
  - ➕ Dodano `EncryptWithEciesUsingPublicKey(byte[] content)` — domyślna metoda szyfrowania ECIES (ECDH + AES-GCM) na krzywej P-256.  
  - 🔧 Metodę `EncryptKsefTokenWithRSAUsingPublicKey(...)` można przełączyć na ECIES lub zachować RSA-OAEP SHA-256 przez parametr `EncryptionMethod`.

- **AuthCoordinator.cs**  
  - 🔧 Sygnatura `AuthKsefTokenAsync(...)` rozszerzona o opcjonalny parametr:
    ```csharp
    EncryptionMethod encryptionMethod = EncryptionMethod.Ecies
    ```  
    — domyślnie ECIES, z możliwością fallback do RSA.

#### 1.2 Core/Models
- **EncryptionMethod.cs**  
  ➕ Nowy enum:
  ```csharp
  public enum EncryptionMethod
  {
      Ecies,
      Rsa
  }
  ````
- **InvoiceSummary.cs** 
  ➕ Dodano nowe pola:
  ```csharp
    public DateTimeOffset IssueDate { get; set; }
    public DateTimeOffset InvoicingDate { get; set; }
    public DateTimeOffset PermanentStorageDate { get; set; }
  ```
- **InvoiceMetadataQueryRequest.cs**  
  🔧 w `Seller` oraz `Buyer` odano nowe typy bez pola `Name`:

#### 1.3 Core/Interfaces

* **ICryptographyService.cs**
  ➕ Dodano metody:

  ```csharp
  byte[] EncryptWithEciesUsingPublicKey(byte[] content);
  void EncryptStreamWithAES256(Stream input, Stream output, byte[] key, byte[] iv);
  ```

* **IAuthCoordinator.cs**
  🔧 `AuthKsefTokenAsync(...)` przyjmuje dodatkowy parametr:

  ```csharp
  EncryptionMethod encryptionMethod = EncryptionMethod.Ecies
  ```

---

### 2. KSeF.Client.Tests

* **AuthorizationTests.cs**
  ➕ Testy end-to-end dla `AuthKsefTokenAsync(...)` w wariantach `Ecies` i `Rsa`.

* **QrCodeTests.cs**
  ➕ Rozbudowano testy `BuildCertificateQr` o scenariusze z ECDSA P-256; poprzednie testy RSA pozostawione zakomentowane.

* **VerificationLinkServiceTests.cs**
  ➕ Dodano testy generowania i weryfikacji linków dla certyfikatów ECDSA P-256.

* **BatchSession.cs**
  ➕ Testy end-to-end dla wysyłki partów z wykorzystaniem strumieni.
---

### 3. KSeF.DemoWebApp/Controllers

* **QrCodeController.cs**
  🔧 Akcja `GetCertificateQr(...)` przyjmuje teraz opcjonalny parametr:

  ```csharp
  string privateKey = ""
  ```

  — jeśli nie jest podany, używany jest osadzony klucz w certyfikacie.

---

```
```
> • 🔀 przeniesione

## Rozwiązania zgłoszonych  - `2025-07-21`

- **#1 Metoda AuthCoordinator.AuthAsync() zawiera błąd**  
  🔧 `KSeF.Client/Api/Services/AuthCoordinator.cs`: usunięto 2 linie zbędnego kodu challenge 

- **#2 Błąd w AuthController.cs**  
  🔧 `KSeF.DemoWebApp/Controllers/AuthController.cs`: poprawiono logikę `AuthStepByStepAsync` (2 additions, 6 deletions) — fallback `contextIdentifier`

- **#3 „Śmieciowa” klasa XadeSDummy**  
  🔀 Przeniesiono `XadeSDummy` z `KSeF.Client.Api.Services` do `WebApplication.Services` (zmiana namespace)
po
- **#4 Optymalizacja RestClient**  
  🔧 `KSeF.Client/Http/RestClient.cs`: uproszczono przeciążenia `SendAsync` (24 additions, 11 deletions), usunięto dead-code, dodano performance benchmark `perf(#4)` 

- **#5 Uporządkowanie języka komunikatów**  
  ➕ `KSeF.Client/Resources/Strings.en.resx` & `Strings.pl.resx`: dodano 101 nowych wpisów w obu plikach; skonfigurowano lokalizację w DI 

- **#6 Wsparcie dla AOT**  
  ➕ `KSeF.Client/KSeF.Client.csproj`: dodano `<PublishAot>`, `<SelfContained>`, `<InvariantGlobalization>`, runtime identifiers `win-x64;linux-x64;osx-arm64`

- **#7 Nadmiarowy plik KSeFClient.csproj**  
  ➖ Usunięto nieużywany plik projektu `KSeFClient.csproj` z repozytorium

---

## Inne zmiany

- **QrCodeService.cs**: ➕ nowa implementacji PNG-QR (`GenerateQrCode`, `ResizePng`, `AddLabelToQrCode`); 

- **PemCertificateInfo.cs**: ➖ Usunięto właściwości PublicKeyPem; 

- **ServiceCollectionExtensions.cs**: ➕ konfiguracjia lokalizacji (`pl-PL`, `en-US`) i rejestracji `IQrCodeService`/`IVerificationLinkService`
- **AuthTokenRequest.cs**: dostosowanie serializacji XML do nowego schematu XSD
- **README.md**: poprawione środowisko w przykładzie rejestracji KSeFClient w kontenerze DI.
---

```
```

## [next-version] – `2025-08-31`
---

### 2. KSeF.Client.Tests

* **Utils**
  ➕ Nowe utils usprawniające autentykację, obsługę sesji interaktywnych, wsadowych oraz zarządzanie uprawnieniami oraz ich metody wspólne: **AuthenticationUtils.cs**, **OnlineSessionUtils.cs**, **MiscellaneousUtils.cs**, **BatchSessionUtils.cs**, **PermissionsUttils.cs**.
  🔧 Refactor testów - użycie nowych klas utils.
  🔧 Zmiana kodu statusu dla zamknięcia sesji interaktywnej z 300 na 170.
  🔧 Zmiana kodu statusu dla zamknięcia sesji wsadowej z 300 na 150.
---

```
```
