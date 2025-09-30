using KSeF.Client.Api.Builders.EntityPermissions;
using KSeF.Client.Api.Builders.X509Certificates;
using KSeF.Client.Core.Models.Authorization;
using KSeF.Client.Core.Models.Permissions.Entity;
using KSeF.Client.Tests.Utils;
using KSeF.Client.Api.Builders.Auth;
using KSeF.Client.Core.Exceptions;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using EntityStandardPermissionType = KSeF.Client.Core.Models.Permissions.Entity.StandardPermissionType;
using PersonSubjectIdentifier = KSeF.Client.Core.Models.Permissions.Person.SubjectIdentifier;
using PersonSubjectIdentifierType = KSeF.Client.Core.Models.Permissions.Person.SubjectIdentifierType;
using StandardPermissionType = KSeF.Client.Core.Models.Permissions.Person.StandardPermissionType;
using KSeF.Client.Core.Models;

namespace KSeF.Client.Tests.Features
{
    [Collection("Authenticate.feature")]
    [Trait("Category", "Features")]
    [Trait("Features", "authenticate.feature")]
    public class AuthenticateTests : KsefIntegrationTestBase
    {
        [Fact]
        [Trait("Scenario", "Uwierzytelnienie za pomocą certyfikatu z identyfikatorem NIP, na uprawnienie właściciel")]
        public async Task GivenOwnerContextAndOwnerPermission_WhenAuthenticatingWithCertificate_ThenAccessTokenReturned()
        {
            var nip = MiscellaneousUtils.GetRandomNip();
            var accessToken = (await AuthenticationUtils.AuthenticateAsync(KsefClient, SignatureService, nip)).AccessToken.Token;

            Assert.NotNull(accessToken);

            var per = GetPerAsStringSet(accessToken);
            Assert.Contains("Owner", per);
        }

        [Theory]
        // ===== pesel =====
        [InlineData("pesel", new StandardPermissionType[] { StandardPermissionType.InvoiceWrite })]
        [InlineData("pesel", new StandardPermissionType[] { StandardPermissionType.InvoiceRead })]
        [InlineData("pesel", new StandardPermissionType[] { StandardPermissionType.CredentialsManage })]
        [InlineData("pesel", new StandardPermissionType[] { StandardPermissionType.CredentialsRead })]
        [InlineData("pesel", new StandardPermissionType[] { StandardPermissionType.Introspection })]
        [InlineData("pesel", new StandardPermissionType[] { StandardPermissionType.SubunitManage })]
        // ===== nip =====
        [InlineData("nip", new StandardPermissionType[] { StandardPermissionType.InvoiceWrite })]
        [InlineData("nip", new StandardPermissionType[] { StandardPermissionType.InvoiceRead })]
        [InlineData("nip", new StandardPermissionType[] { StandardPermissionType.CredentialsManage })]
        [InlineData("nip", new StandardPermissionType[] { StandardPermissionType.CredentialsRead })]
        [InlineData("nip", new StandardPermissionType[] { StandardPermissionType.Introspection })]
        [InlineData("nip", new StandardPermissionType[] { StandardPermissionType.SubunitManage })]
        [Trait("Scenario", "Uwierzytelnienie certyfikatem (PESEL/NIP) na różne uprawnienia")]
        public async Task GivenOwnerContextAndPermissionGranted_WhenAuthenticatingAsSubject_ThenAccessTokenReturned(
            string identifierKind,
            StandardPermissionType[] permissions,
            ContextIdentifierType contextIdentifierType = ContextIdentifierType.Nip)
        {
            var ownerNip = MiscellaneousUtils.GetRandomNip();
            var ownerToken = (await AuthenticationUtils.AuthenticateAsync(KsefClient, SignatureService, ownerNip)).AccessToken.Token;

            var delegateNip = MiscellaneousUtils.GetRandomNip();
            var pesel = MiscellaneousUtils.GetRandomPesel();

            PersonSubjectIdentifier subjectIdentifier;
            if (identifierKind.Equals("pesel", StringComparison.OrdinalIgnoreCase))
            {
                subjectIdentifier = new PersonSubjectIdentifier { Type = PersonSubjectIdentifierType.Pesel, Value = pesel };
            }
            else
            {
                subjectIdentifier = new PersonSubjectIdentifier { Type = PersonSubjectIdentifierType.Nip, Value = delegateNip };
            }
            await PermissionsUtils.GrantPersonPermissionsAsync(KsefClient, ownerToken, subjectIdentifier, permissions);

            var challengeResponse = await KsefClient
                .GetAuthChallengeAsync();

            var authTokenRequest = AuthTokenRequestBuilder
               .Create()
               .WithChallenge(challengeResponse.Challenge)
               .WithContext(contextIdentifierType, ownerNip)
               .WithIdentifierType(SubjectIdentifierTypeEnum.CertificateSubject)
               .WithAuthorizationPolicy(new AuthorizationPolicy { /* ... */ })
               .Build();

            var unsignedXml = authTokenRequest.SerializeToXmlString();

            using var certificate = identifierKind.Equals("pesel", StringComparison.OrdinalIgnoreCase)
                ? SelfSignedCertificateForSignatureBuilder
                    .Create()
                    .WithGivenName("A")
                    .WithSurname("R")
                    .WithSerialNumber("PNOPL-" + pesel)
                    .WithCommonName("A R")
                    .Build()
                : SelfSignedCertificateForSignatureBuilder
                    .Create()
                    .WithGivenName("Jan")
                    .WithSurname("Kowalski")
                    .WithSerialNumber("TINPL-" + delegateNip)
                    .WithCommonName("Jan Kowalski")
                    .Build();
            var signedXml = SignatureService.Sign(unsignedXml, certificate);
            await Task.Delay(1500);

            var authOperationInfo = await KsefClient
              .SubmitXadesAuthRequestAsync(signedXml, false, CancellationToken.None);
            await Task.Delay(1500);

            var status = await EnsureAuthenticationCompletedAsync(
                KsefClient,
                authOperationInfo.ReferenceNumber,
                authOperationInfo.AuthenticationToken.Token);

            Assert.Equal(200, status.Status.Code);

            var accessToken = await KsefClient.GetAccessTokenAsync(authOperationInfo.AuthenticationToken.Token);
            Assert.NotNull(accessToken);
            var actual = GetPerAsEnumSet<StandardPermissionType>(accessToken.AccessToken.Token);
            Assert.True(permissions.ToHashSet().IsSubsetOf(actual));
        }

        [Theory]
        // ===== nip =====
        [InlineData(EntityStandardPermissionType.InvoiceRead)]
        [InlineData(EntityStandardPermissionType.InvoiceWrite)]
        [Trait("Scenario", "Uwierzytelnienie za pomocą pieczęci z nip, na różne uprawnienia")]
        public async Task GivenOwnerContextAndPermissionGranted_WhenAuthenticatingAsSubjectEntity_ThenAccessTokenReturned(
            EntityStandardPermissionType permission,
            ContextIdentifierType contextIdentifierType = ContextIdentifierType.Nip)
        {
            var ownerNip = MiscellaneousUtils.GetRandomNip();
            var ownerToken = (await AuthenticationUtils.AuthenticateAsync(KsefClient, SignatureService, ownerNip)).AccessToken.Token;

            var delegateNip = MiscellaneousUtils.GetRandomNip();

            Core.Models.Permissions.Entity.SubjectIdentifier subject = new Core.Models.Permissions.Entity.SubjectIdentifier
            { Type = Core.Models.Permissions.Entity.SubjectIdentifierType.Nip, Value = delegateNip };

            var request = GrantEntityPermissionsRequestBuilder
                .Create()
                .WithSubject(subject)
                .WithPermissions(Permission.New(permission, true))
                .WithDescription($"Grant {string.Join(", ", permission)} to {subject.Type}:{subject.Value}")
                .Build();

            await KsefClient.GrantsPermissionEntityAsync(request, ownerToken);

            var challengeResponse = await KsefClient
                .GetAuthChallengeAsync();

            var authTokenRequest = AuthTokenRequestBuilder
               .Create()
               .WithChallenge(challengeResponse.Challenge)
               .WithContext(contextIdentifierType, ownerNip)
               .WithIdentifierType(SubjectIdentifierTypeEnum.CertificateSubject)
               .WithAuthorizationPolicy(new AuthorizationPolicy { /* ... */ })
               .Build();

            var unsignedXml = authTokenRequest.SerializeToXmlString();

            using var certificate = SelfSignedCertificateForSealBuilder
                .Create()
                .WithOrganizationName("AR sp. z o.o")
                .WithOrganizationIdentifier("VATPL-" + delegateNip)
                .WithCommonName("A R")
                .Build();

            var signedXml = SignatureService.Sign(unsignedXml, certificate);
            await Task.Delay(1500);

            var authOperationInfo = await KsefClient
              .SubmitXadesAuthRequestAsync(signedXml, false, CancellationToken.None);
            await Task.Delay(1500);

            var status = await EnsureAuthenticationCompletedAsync(
                KsefClient,
                authOperationInfo.ReferenceNumber,
                authOperationInfo.AuthenticationToken.Token);

            Assert.Equal(200, status.Status.Code);

            var token = await KsefClient.GetAccessTokenAsync(authOperationInfo.AuthenticationToken.Token);
            Assert.NotNull(token);
            var entitySet = GetPerAsEnumSet<EntityStandardPermissionType>(token.AccessToken.Token);
            Assert.Contains(permission, entitySet);
        }

        [Fact]
        [Trait("Scenario", "Uwierzytelnienie za pomocą PESEL oraz niepoprawnego certyfikatu z PESEL")]
        public async Task GivenOwnerContextAndWrongCertificate_WhenAuthenticateWithPESEL_ThenError()
        {
            var ownerNip = MiscellaneousUtils.GetRandomNip();
            var ownerToken = (await AuthenticationUtils.AuthenticateAsync(KsefClient, SignatureService, ownerNip)).AccessToken.Token;

            var delegateNip = MiscellaneousUtils.GetRandomNip();
            var pesel = MiscellaneousUtils.GetRandomPesel();

            var challengeResponse = await KsefClient
                .GetAuthChallengeAsync();

            var authTokenRequest = AuthTokenRequestBuilder
               .Create()
               .WithChallenge(challengeResponse.Challenge)
               .WithContext(ContextIdentifierType.Nip, ownerNip)
               .WithIdentifierType(SubjectIdentifierTypeEnum.CertificateSubject)
               .WithAuthorizationPolicy(new AuthorizationPolicy { /* ... */ })
               .Build();

            var unsignedXml = authTokenRequest.SerializeToXmlString();

            //błąd w certyfikacie
            using var certificate = SelfSignedCertificateForSignatureBuilder
               .Create()
               .WithGivenName("A")
               .WithSurname("R")
               .WithSerialNumber("-" + pesel)
               .WithCommonName("A R")
               .Build();

            var signedXml = SignatureService.Sign(unsignedXml, certificate);

            var ex = await Assert.ThrowsAsync<KsefApiException>(async () =>
            {
                var authOperationInfo = await KsefClient
              .SubmitXadesAuthRequestAsync(signedXml, false, CancellationToken.None);
            });
        }

        [Fact]
        [Trait("Scenario", "Niepoprawne uwierzytelnienia - brak żądania autoryzacyjnego")]
        public async Task GivenOwnerContextAndNip_WhenAuthenticatingWithWrongData_ThenError()
        {
            var nip = MiscellaneousUtils.GetRandomNip();

            // brak żądania autoryzacyjnego
            var unsignedXml = AuthTokenRequestSerializer.SerializeToXmlString(null);

            using var certificate = SelfSignedCertificateForSignatureBuilder
                   .Create()
                   .WithGivenName("Jan")
                   .WithSurname("Kowalski")
                   .WithSerialNumber("TINPL-" + nip)
                   .WithCommonName("Jan Kowalski")
                   .Build();

            var signedXml = SignatureService.Sign(unsignedXml, certificate);
            var ex = await Assert.ThrowsAsync<KsefApiException>(async () =>
            {
                var authOperationInfo = await KsefClient
              .SubmitXadesAuthRequestAsync(signedXml, false, CancellationToken.None);
            });
        }

        [Fact]
        [Trait("Scenario", "Niepoprawne uwierzytelnienia - zły nip w podpisie")]
        public async Task GivenOwnerContext_WhenAuthenticatingWithWrongNip_ThenError()
        {
            var ownerNip = MiscellaneousUtils.GetRandomNip();

            var challengeResponse = await KsefClient
                            .GetAuthChallengeAsync();

            var authTokenRequest = AuthTokenRequestBuilder
               .Create()
               .WithChallenge(challengeResponse.Challenge)
               .WithContext(ContextIdentifierType.Nip, ownerNip)
               .WithIdentifierType(SubjectIdentifierTypeEnum.CertificateSubject)
               .WithAuthorizationPolicy(new AuthorizationPolicy { /* ... */ })
               .Build();

            var unsignedXml = authTokenRequest.SerializeToXmlString();

            //niepoprawny nip
            using var certificate = SelfSignedCertificateForSignatureBuilder
                   .Create()
                   .WithGivenName("Jan")
                   .WithSurname("Kowalski")
                   .WithSerialNumber("TINPL-111111")
                   .WithCommonName("Jan Kowalski")
                   .Build();

            var signedXml = SignatureService.Sign(unsignedXml, certificate);

            var ex = await Assert.ThrowsAsync<KsefApiException>(async () =>
            {
                var authOperationInfo = await KsefClient
              .SubmitXadesAuthRequestAsync(signedXml, false, CancellationToken.None);
            });
        }

        [Fact]
        [Trait("Scenario", "Niepoprawne uwierzytelnienia - błędny plik autoryzacyjny")]
        public async Task GivenOwnerContext_WhenAuthenticatingWithWrongAuthenticateFile_ThenError()
        {
            var ownerNip = MiscellaneousUtils.GetRandomNip();
            var nip = MiscellaneousUtils.GetRandomNip();

            var challengeResponse = await KsefClient
                            .GetAuthChallengeAsync();

            var authTokenRequest = AuthTokenRequestBuilder
               .Create()
               .WithChallenge(challengeResponse.Challenge)
               .WithContext(ContextIdentifierType.Nip, ownerNip)
               .WithIdentifierType(SubjectIdentifierTypeEnum.CertificateSubject)
               .WithAuthorizationPolicy(new AuthorizationPolicy { /* ... */ })
               .Build();

            var unsignedXml = authTokenRequest.SerializeToXmlString();

            using var certificate = SelfSignedCertificateForSignatureBuilder
                   .Create()
                   .WithGivenName("Jan")
                   .WithSurname("Kowalski")
                   .WithSerialNumber("TINPL-" + nip)
                   .WithCommonName("Jan Kowalski")
                   .Build();

            Assert.Throws<ArgumentException>(() => SignatureService.Sign(string.Empty, certificate));
        }

        private static async Task<AuthStatus> EnsureAuthenticationCompletedAsync(
            IKSeFClient client,
            string operationReferenceNumber,
            string authToken,
            TimeSpan? timeout = null,
            TimeSpan? pollInterval = null,
            CancellationToken ct = default)
        {
            timeout ??= TimeSpan.FromSeconds(60);
            pollInterval ??= TimeSpan.FromSeconds(1);

            var startTime = DateTime.UtcNow;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout.Value);

            AuthStatus status;
            do
            {
                status = await client.GetAuthStatusAsync(operationReferenceNumber, authToken, cts.Token);

                Console.WriteLine(
                    $"Polling: StatusCode={status.Status.Code}, " +
                    $"Description='{status.Status.Description}', " +
                    $"Elapsed={DateTime.UtcNow - startTime:mm\\:ss}");

                if (status.Status.Code != 200)
                {
                    await Task.Delay(pollInterval.Value, cts.Token);
                }
            }
            while (status.Status.Code == 100 && DateTime.UtcNow - startTime < timeout);

            if (status.Status.Code != 200)
            {
                throw new Exception(
                    $"Authentication failed. Status code: {status.Status.Code}, description: {status.Status.Description}.");
            }

            return status;
        }
        private static HashSet<string> GetPerAsStringSet(string jwtToken)
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(jwtToken);

            var perClaims = jwt.Claims
                .Where(c => c.Type == "per")
                .Select(c => c.Value)
                .ToArray();

            if (perClaims.Length == 1 && perClaims[0].TrimStart().StartsWith("["))
            {
                var arr = JsonSerializer.Deserialize<string[]>(perClaims[0]) ?? Array.Empty<string>();
                return arr.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            return perClaims.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static HashSet<TEnum> GetPerAsEnumSet<TEnum>(string jwtToken)
              where TEnum : struct, Enum
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(jwtToken);
            var perValues = jwt.Claims.Where(c => c.Type == "per").Select(c => c.Value).ToArray();

            IEnumerable<string> raw =
                perValues.Length == 1 && perValues[0].TrimStart().StartsWith("[")
                    ? JsonSerializer.Deserialize<string[]>(perValues[0]) ?? Array.Empty<string>()
                    : perValues;

            var set = new HashSet<TEnum>();
            foreach (var s in raw)
                if (Enum.TryParse<TEnum>(s, ignoreCase: true, out var e))
                    set.Add(e);

            return set;
        }
    }
}
