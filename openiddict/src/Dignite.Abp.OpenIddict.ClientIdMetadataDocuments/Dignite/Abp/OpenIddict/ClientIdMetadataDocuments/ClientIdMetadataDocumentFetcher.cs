using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Abp.OpenIddict.ClientIdMetadataDocuments;

internal enum ClientIdMetadataDocumentFetchStatus
{
    Ok,

    /// <summary>Fetched, but not acceptable (4xx, a redirect, bad content). An existing registration must not be reused.</summary>
    Invalid,

    /// <summary>Not fetched for now (timeout, 5xx, network error). An existing registration may be reused.</summary>
    Unavailable
}

internal sealed record ClientIdMetadataDocumentFetchResult(
    ClientIdMetadataDocumentFetchStatus Status,
    ClientIdMetadataDocument? Document,
    TimeSpan CacheLifetime,
    string? Error)
{
    public static ClientIdMetadataDocumentFetchResult Invalid(string error) =>
        new(ClientIdMetadataDocumentFetchStatus.Invalid, null, TimeSpan.Zero, error);

    public static ClientIdMetadataDocumentFetchResult Unavailable(string error) =>
        new(ClientIdMetadataDocumentFetchStatus.Unavailable, null, TimeSpan.Zero, error);
}

/// <summary>
/// Fetches the metadata document at a client_id URL. An authorization server fetching a URL a stranger chose is a
/// textbook SSRF entry point (both the specification and the draft name it), so: https only, no redirects, no proxy,
/// the host resolved before connecting and refused if any address is private, loopback or reserved, a time limit and
/// a size limit.
/// </summary>
internal class ClientIdMetadataDocumentFetcher
{
    public const string HttpClientName = "Dignite.Abp.OpenIddict.ClientIdMetadataDocuments";

    private readonly HttpClient _httpClient;
    private readonly AbpClientIdMetadataDocumentOptions _options;

    public ClientIdMetadataDocumentFetcher(HttpClient httpClient, AbpClientIdMetadataDocumentOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public virtual async Task<ClientIdMetadataDocumentFetchResult> FetchAsync(string clientId, CancellationToken cancellationToken)
    {
        Uri uri;
        try
        {
            uri = ClientIdMetadataDocumentRules.ParseClientIdUrl(clientId);
        }
        catch (ClientIdMetadataDocumentException ex)
        {
            return ClientIdMetadataDocumentFetchResult.Invalid(ex.Message);
        }

        if (_options.AllowedHosts.Count > 0
            && !_options.AllowedHosts.Any(h => string.Equals(h, uri.IdnHost, StringComparison.OrdinalIgnoreCase)))
        {
            return ClientIdMetadataDocumentFetchResult.Invalid($"Clients hosted on '{uri.IdnHost}' are not accepted by this server.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.FetchTimeout);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            if ((int)response.StatusCode >= 500)
            {
                return ClientIdMetadataDocumentFetchResult.Unavailable($"The client metadata document returned HTTP {(int)response.StatusCode}.");
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                // 3xx lands here too: redirects are not followed, the document must be at the client_id URL itself.
                return ClientIdMetadataDocumentFetchResult.Invalid($"The client metadata document returned HTTP {(int)response.StatusCode}.");
            }

            if (response.Content.Headers.ContentLength > _options.MaxDocumentBytes)
            {
                return ClientIdMetadataDocumentFetchResult.Invalid("The client metadata document is too large.");
            }

            var body = await ReadLimitedAsync(response.Content, _options.MaxDocumentBytes, timeout.Token);
            if (body == null)
            {
                return ClientIdMetadataDocumentFetchResult.Invalid("The client metadata document is too large.");
            }

            var document = ClientIdMetadataDocumentRules.Parse(clientId, body);
            return new ClientIdMetadataDocumentFetchResult(
                ClientIdMetadataDocumentFetchStatus.Ok, document, GetCacheLifetime(response.Headers.CacheControl), null);
        }
        catch (ClientIdMetadataDocumentException ex)
        {
            return ClientIdMetadataDocumentFetchResult.Invalid(ex.Message);
        }
        catch (BlockedAddressException ex)
        {
            return ClientIdMetadataDocumentFetchResult.Invalid(ex.Message);
        }
        catch (HttpRequestException ex) when (ex.InnerException is BlockedAddressException blocked)
        {
            return ClientIdMetadataDocumentFetchResult.Invalid(blocked.Message);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException
                                   && !cancellationToken.IsCancellationRequested)
        {
            return ClientIdMetadataDocumentFetchResult.Unavailable("The client metadata document could not be retrieved.");
        }
    }

    /// <summary>
    /// The registration lives as long as the response's Cache-Control says (the specification: SHOULD respect HTTP
    /// cache headers), clamped to the configured bounds.
    /// </summary>
    public TimeSpan GetCacheLifetime(CacheControlHeaderValue? cacheControl)
    {
        TimeSpan lifetime;
        if (cacheControl == null)
        {
            lifetime = _options.DefaultCacheLifetime;
        }
        else if (cacheControl.NoStore || cacheControl.NoCache)
        {
            lifetime = _options.MinCacheLifetime;
        }
        else
        {
            lifetime = cacheControl.MaxAge ?? _options.DefaultCacheLifetime;
        }

        if (lifetime < _options.MinCacheLifetime)
        {
            return _options.MinCacheLifetime;
        }

        return lifetime > _options.MaxCacheLifetime ? _options.MaxCacheLifetime : lifetime;
    }

    private static async Task<byte[]?> ReadLimitedAsync(HttpContent content, int maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[maxBytes + 1];
        var total = 0;
        int read;
        while (total < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken)) > 0)
        {
            total += read;
        }

        return total > maxBytes ? null : buffer[..total];
    }

    /// <summary>
    /// The handler documents are fetched with: it resolves the host itself and connects only to public addresses (so a
    /// DNS name pointing inside cannot get through), follows no redirect and uses no proxy (through a proxy, the
    /// connect callback would only see the proxy's address).
    /// </summary>
    public static SocketsHttpHandler CreateGuardedHandler(AbpClientIdMetadataDocumentOptions options)
    {
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = options.FetchTimeout,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectCallback = async (context, cancellationToken) =>
            {
                var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
                var allowed = addresses.Where(IsPublicAddress).ToList();
                if (allowed.Count == 0 || allowed.Count != addresses.Length)
                {
                    throw new BlockedAddressException($"'{context.DnsEndPoint.Host}' does not resolve to a public address.");
                }

                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(allowed.ToArray(), context.DnsEndPoint.Port, cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };
    }

    /// <summary>
    /// Public unicast addresses only: loopback, private, link-local, carrier-grade NAT, documentation and benchmark
    /// ranges, multicast and reserved ranges are refused.
    /// </summary>
    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.Broadcast) || address.Equals(IPAddress.IPv6None))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 0
                     || b[0] == 10
                     || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)   // 100.64.0.0/10 carrier-grade NAT
                     || b[0] == 127
                     || (b[0] == 169 && b[1] == 254)                // link-local, cloud metadata endpoints included
                     || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                     || (b[0] == 192 && b[1] == 0 && b[2] == 0)
                     || (b[0] == 192 && b[1] == 0 && b[2] == 2)
                     || (b[0] == 192 && b[1] == 168)
                     || (b[0] == 198 && (b[1] == 18 || b[1] == 19))
                     || (b[0] == 198 && b[1] == 51 && b[2] == 100)
                     || (b[0] == 203 && b[1] == 0 && b[2] == 113)
                     || b[0] >= 224);                                // multicast and reserved
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();
            return !(address.IsIPv6LinkLocal
                     || address.IsIPv6SiteLocal
                     || address.IsIPv6Multicast
                     || (b[0] & 0xFE) == 0xFC                        // fc00::/7 unique local
                     || (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0D && b[3] == 0xB8) // 2001:db8::/32 documentation
                     || (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x00 && b[3] == 0x00) // 2001::/32 Teredo
                     || (b[0] == 0x20 && b[1] == 0x02)               // 2002::/16 6to4: the embedded IPv4 is not checked, so refused
                     || (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xFF && b[3] == 0x9B)); // 64:ff9b::/96 NAT64
        }

        return false;
    }

    public sealed class BlockedAddressException : Exception
    {
        public BlockedAddressException(string message)
            : base(message)
        {
        }
    }
}
