using KSeF.Client.Core.Models.Sessions;
using KSeF.Client;
using Microsoft.AspNetCore.Mvc;


namespace WebApplication.Controllers;

[Route("[controller]")]
[ApiController]
public class SessionController : ControllerBase
{

    private readonly IKSeFClient ksefClient;

    public SessionController(IKSeFClient ksefClient)
    {
        this.ksefClient = ksefClient;
    }

    [HttpGet("online-sessions")]
    public async Task<ActionResult<ICollection<Session>>> GetOnlineSessionsAsync([FromForm] string accessToken, [FromForm] SessionsFilter sessionsFilter, CancellationToken cancellationToken)
    {
        var sessions = new List<Session>();
        const int pageSize = 20;
        string? continuationToken = null;
        do
        {
            var response = await ksefClient.GetSessionsAsync(SessionType.Online, accessToken, pageSize, continuationToken, sessionsFilter, cancellationToken);
            continuationToken = response.ContinuationToken;
            sessions.AddRange(response.Sessions);
        } while (!string.IsNullOrEmpty(continuationToken));

        return Ok(sessions);
    }

    [HttpGet("batch-sessions")]
    public async Task<ActionResult<ICollection<Session>>> GetbatchSessionsAsync([FromForm] string accessToken, [FromForm] SessionsFilter sessionsFilter, CancellationToken cancellationToken)
    {
        var sessions = new List<Session>();
        const int pageSize = 20;
        string? continuationToken = null;
        do
        {
            var response = await ksefClient.GetSessionsAsync(SessionType.Batch, accessToken, pageSize, continuationToken, sessionsFilter, cancellationToken);
            continuationToken = response.ContinuationToken;
            sessions.AddRange(response.Sessions);
        } while (!string.IsNullOrEmpty(continuationToken));

        return Ok(sessions);
    }


    [HttpGet("status")]
    public async Task<SessionStatusResponse> GetStatusAsync(string sessionReferenceNumber, string accessToken, CancellationToken cancellationToken)
    {
        var status = await ksefClient.GetSessionStatusAsync(sessionReferenceNumber, accessToken, cancellationToken)
            .ConfigureAwait(false);
        return status;
    }

    [HttpGet("invoice-upo-by-ksef-number")]
    public async Task<string> GetInvoiceUpoByKsefNumberAsync(string sessionReferenceNumber, string ksefNumber, string accessToken, CancellationToken cancellationToken)
    {
        var upo = await ksefClient.GetSessionInvoiceUpoByKsefNumberAsync(sessionReferenceNumber, ksefNumber, accessToken, cancellationToken)
            .ConfigureAwait(false);
        return upo;
    }

    [HttpGet("session-upo")]
    public async Task<string> GetSessionUpoAsync(string sessionReferenceNumber, string upoReferenceNumber, string accessToken, CancellationToken cancellationToken)
    {
        var upo = await ksefClient.GetSessionUpoAsync(sessionReferenceNumber, upoReferenceNumber, accessToken, cancellationToken)
            .ConfigureAwait(false);
        return upo;
    }

    [HttpGet("session-documents")]
    public async Task<ActionResult<SessionInvoicesResponse>> GetSessionDocumentsAsync(string accessToken, string sessionReferenceNumber, CancellationToken cancellationToken)
    {
        var sessionDocuments = await ksefClient.GetSessionInvoicesAsync(sessionReferenceNumber, accessToken, null, null, cancellationToken);
        return Ok(sessionDocuments);
    }

    [HttpGet("failed-invoices")]
    public async Task<ActionResult<SessionFailedInvoicesResponse>> GetFailedInvoicesAsync(string accessToken, string sessionReferenceNumber, CancellationToken cancellationToken)
    {
        var failedInvoices = await ksefClient.GetSessionFailedInvoicesAsync(sessionReferenceNumber, accessToken, null, null, cancellationToken);
        return Ok(failedInvoices);
    }

    [HttpGet("invoice-upo-by-invoice-reference-number")]
    public async Task<string> GetInvoiceUpoByReferenceNumberAsync(string sessionReferenceNumber, string ksefNumber, string accessToken, CancellationToken cancellationToken)
    {
        var upo = await ksefClient.GetSessionInvoiceUpoByReferenceNumberAsync(sessionReferenceNumber, ksefNumber, accessToken, cancellationToken)
            .ConfigureAwait(false);
        return upo;
    }

}
