using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Abp.OpenIddict.ClientIdMetadataDocuments;

/// <summary>Serves documents from memory, through the real parsing rules, and counts the fetches.</summary>
internal sealed class StubClientIdMetadataDocumentFetcher : ClientIdMetadataDocumentFetcher
{
    private readonly ConcurrentDictionary<string, Func<ClientIdMetadataDocumentFetchResult>> _responses = new();
    private int _fetchCount;

    public StubClientIdMetadataDocumentFetcher()
        : base(new HttpClient(), new AbpClientIdMetadataDocumentOptions())
    {
    }

    public int FetchCount => _fetchCount;

    public void SetDocument(string clientId, string json, TimeSpan? cacheLifetime = null) =>
        _responses[clientId] = () => new ClientIdMetadataDocumentFetchResult(
            ClientIdMetadataDocumentFetchStatus.Ok,
            ClientIdMetadataDocumentRules.Parse(clientId, Encoding.UTF8.GetBytes(json)),
            cacheLifetime ?? TimeSpan.FromHours(1),
            null);

    public void SetUnavailable(string clientId) =>
        _responses[clientId] = () => ClientIdMetadataDocumentFetchResult.Unavailable("The client metadata document could not be retrieved.");

    public void SetInvalid(string clientId, string error) =>
        _responses[clientId] = () => ClientIdMetadataDocumentFetchResult.Invalid(error);

    public override Task<ClientIdMetadataDocumentFetchResult> FetchAsync(string clientId, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _fetchCount);
        try
        {
            return Task.FromResult(_responses.TryGetValue(clientId, out var respond)
                ? respond()
                : ClientIdMetadataDocumentFetchResult.Invalid("The client metadata document returned HTTP 404."));
        }
        catch (ClientIdMetadataDocumentException ex)
        {
            return Task.FromResult(ClientIdMetadataDocumentFetchResult.Invalid(ex.Message));
        }
    }
}
