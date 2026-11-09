using System;
using System.Collections.Generic;

namespace Dignite.Abp.OpenIddict.ClientIdMetadataDocuments;

/// <summary>
/// How the authorization server accepts OAuth Client ID Metadata Documents (CIMD): a client uses an HTTPS URL as its
/// <c>client_id</c>, and the server fetches the JSON document at that URL to learn the client's name and redirect URIs.
/// </summary>
public class AbpClientIdMetadataDocumentOptions
{
    /// <summary>
    /// Whether an https <c>client_id</c> is resolved as a metadata document, and the discovery document says so.
    /// Default: <see langword="true"/>.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// The scopes a CIMD client may request, granted as <c>scp:</c> permissions. Required: the application refuses to
    /// start while <see cref="IsEnabled"/> is set and this is empty. <c>openid</c> and <c>offline_access</c> need no
    /// entry; OpenIddict never checks permissions for them.
    /// </summary>
    public List<string> Scopes { get; } = new();

    /// <summary>
    /// The hosts a <c>client_id</c> may be on, compared exactly (no subdomains) with the URL's IDN host. Empty accepts
    /// any public host - the "open server" policy. Either way, a host that resolves to a private, loopback or reserved
    /// address is refused when the document is fetched.
    /// </summary>
    public List<string> AllowedHosts { get; } = new();

    /// <summary>How long fetching a document may take, connecting included. Default: 5 seconds.</summary>
    public TimeSpan FetchTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>The largest document accepted, in bytes. Default: 5 KB, the draft's suggested limit.</summary>
    public int MaxDocumentBytes { get; set; } = 5 * 1024;

    /// <summary>
    /// The shortest time a registration is trusted before the document is fetched again, whatever the response's
    /// <c>Cache-Control</c> says (<c>no-store</c> and <c>no-cache</c> included). Default: 5 minutes.
    /// </summary>
    public TimeSpan MinCacheLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>The longest time a registration is trusted, whatever <c>max-age</c> says. Default: 24 hours.</summary>
    public TimeSpan MaxCacheLifetime { get; set; } = TimeSpan.FromHours(24);

    /// <summary>The time a registration is trusted when the response has no <c>max-age</c>. Default: 1 hour.</summary>
    public TimeSpan DefaultCacheLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Whether <c>private_key_jwt</c> is taken out of the discovery document's
    /// <c>token_endpoint_auth_methods_supported</c>. A client that sees it there may switch to a confidential-client
    /// metadata document and authenticate with a signed assertion, which a CIMD registration here does not accept.
    /// This changes the whole discovery document, so a server whose pre-registered confidential clients use
    /// <c>private_key_jwt</c> turns it off. Default: <see langword="true"/>.
    /// </summary>
    public bool RemovePrivateKeyJwtFromDiscovery { get; set; } = true;

    /// <summary>
    /// Whether a background worker deletes CIMD registrations that have expired and hold no authorization or token.
    /// With no <see cref="AllowedHosts"/>, anyone who publishes a valid document can add a registration; this keeps
    /// them from piling up. It does not run when ABP's background workers are disabled. Default: <see langword="true"/>.
    /// </summary>
    public bool IsCleanupEnabled { get; set; } = true;

    /// <summary>How often the cleanup worker runs. Default: 1 hour.</summary>
    public TimeSpan CleanupPeriod { get; set; } = TimeSpan.FromHours(1);
}
