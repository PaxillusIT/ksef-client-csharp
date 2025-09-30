using KSeF.Client.Core.Models.Permissions;
using KSeF.Client.Core.Models.Permissions.Person;
using KSeF.Client.Tests.Utils;

namespace KSeF.Client.Tests.Features;

public partial class CredentialsRevokeTests
{
    /// <summary>
    /// Pomocnicza klasa do testów unieważniania i nadawania uprawnień (Credentials).
    /// Zawiera metody opakowujące wywołania API oraz ułatwiające sprawdzanie statusu operacji.
    /// </summary>
    private class CredentialsRevokeHelpers
    {
        /// <summary>
        /// Wyszukuje uprawnienia nadane osobom fizycznym w bieżącym kontekście, filtrowane po stanie uprawnienia.
        /// </summary>
        /// <param name="client">Klient KSeF używany do wywołań API.</param>
        /// <param name="token">Token dostępu używany do autoryzacji.</param>
        /// <param name="state">Stan uprawnienia do filtrowania (np. aktywne/nieaktywne).</param>
        /// <returns>Listę uprawnień osoby w formie tylko do odczytu.</returns>
        public static async Task<IReadOnlyList<PersonPermission>> SearchPersonPermissionsAsync(
        IKSeFClient client, string token, PermissionState state
            )
        => await PermissionsUtils.SearchPersonPermissionsAsync(
               client,
               token,
               QueryTypeEnum.PermissionsGrantedInCurrentContext,
               state);

        /// <summary>
        /// Nadaje uprawnienie CredentialsManage delegatowi zidentyfikowanemu przez NIP.
        /// </summary>
        /// <param name="client">Klient KSeF używany do wywołań API.</param>
        /// <param name="ownerToken">Token właściciela uprawnień (nadawcy).</param>
        /// <param name="delegateNip">NIP delegata, któremu zostanie nadane uprawnienie.</param>
        /// <returns>Prawda, jeśli operacja zakończyła się powodzeniem.</returns>
        public static async Task<bool> GrantCredentialsManageToDelegateAsync(
            IKSeFClient client, string ownerToken, string delegateNip)
        {
            var subjectIdentifier = new Client.Core.Models.Permissions.Person.SubjectIdentifier { Type = SubjectIdentifierType.Nip, Value = delegateNip };
            var permissions = new[] { StandardPermissionType.CredentialsManage };

            var operationResponse = await PermissionsUtils.GrantPersonPermissionsAsync(client, ownerToken, subjectIdentifier, permissions);

            return await ConfirmOperationSuccessAsync(client, operationResponse, ownerToken);
        }

        /// <summary>
        /// Odbiera (unieważnia) wskazane uprawnienie osoby po jego identyfikatorze.
        /// </summary>
        /// <param name="client">Klient KSeF używany do wywołań API.</param>
        /// <param name="token">Token dostępu używany do autoryzacji.</param>
        /// <param name="permissionId">Identyfikator uprawnienia do unieważnienia.</param>
        /// <returns>Prawda, jeśli operacja zakończyła się powodzeniem.</returns>
        public static async Task<bool> RevokePersonPermissionAsync(
            IKSeFClient client, string token, string permissionId)
        {
            var operationResponse = await PermissionsUtils.RevokePersonPermissionAsync(client, token, permissionId);

            return await ConfirmOperationSuccessAsync(client, operationResponse, token);
        }

        /// <summary>
        /// Nadaje uprawnienie InvoiceWrite osobie z PESEL-em w trybie pośrednim
        /// (subject: PESEL, target: NIP właściciela), korzystając z tokena delegata.
        /// </summary>
        /// <param name="client">Klient KSeF używany do wywołań API.</param>
        /// <param name="delegateToken">Token delegata posiadającego prawo nadawania uprawnień.</param>
        /// <param name="nipOwner">NIP właściciela (target), w którego kontekście nadawane jest uprawnienie.</param>
        /// <param name="pesel">PESEL osoby, której nadawane jest uprawnienie.</param>
        /// <returns>Prawda, jeśli operacja zakończyła się powodzeniem.</returns>
        public static async Task<bool> GrantInvoiceWriteToPeselAsManagerAsync(
            IKSeFClient client, string delegateToken, string nipOwner, string pesel)
        {
            var subjectIdentifier = new Core.Models.Permissions.IndirectEntity.SubjectIdentifier
            {
                Type = Core.Models.Permissions.IndirectEntity.SubjectIdentifierType.Pesel,
                Value = pesel
            };

            var targetIdentifier = new Core.Models.Permissions.IndirectEntity.TargetIdentifier
            {
                Type = Core.Models.Permissions.IndirectEntity.TargetIdentifierType.Nip,
                Value = nipOwner
            };

            var permissions = new[] { Core.Models.Permissions.IndirectEntity.StandardPermissionType.InvoiceWrite };

            var operationResponse = await PermissionsUtils.GrantIndirectPermissionsAsync(client, delegateToken, subjectIdentifier, targetIdentifier, permissions);

            return await ConfirmOperationSuccessAsync(client, operationResponse, delegateToken);
        }

        /// <summary>
        /// Pomocnicza metoda potwierdzająca powodzenie operacji nadawania/odbierania uprawnień.
        /// Czeka krótką chwilę, a następnie sprawdza status operacji po numerze referencyjnym.
        /// </summary>
        /// <param name="client">Klient KSeF używany do wywołań API.</param>
        /// <param name="operationResponse">Odpowiedź inicjująca operację (z numerem referencyjnym).</param>
        /// <param name="token">Token dostępu używany do autoryzacji odczytu statusu operacji.</param>
        /// <returns>Prawda, jeżeli status operacji zwróci kod 200.</returns>
        private static async Task<bool> ConfirmOperationSuccessAsync(
            IKSeFClient client, OperationResponse operationResponse, string token)
        {
            if (string.IsNullOrWhiteSpace(operationResponse?.OperationReferenceNumber))
                return false;

            // Krótkie odczekanie, aby backend zdążył przetworzyć operację
            await Task.Delay(1000);

            var status = await PermissionsUtils.GetPermissionsOperationStatusAsync(client, operationResponse.OperationReferenceNumber!, token);
            return status?.Status?.Code == 200;
        }
    }
}