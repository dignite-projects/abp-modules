using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.Notifications.Push.Expo;

/// <summary>
/// Sends through the Expo Push Service, which forwards to APNs and FCM with the credentials uploaded to the Expo
/// project. Plain <see cref="HttpClient"/>: Expo publishes no .NET SDK, and the API is one POST.
/// </summary>
/// <remarks>
/// <para>
/// Only push <i>tickets</i> are read. Expo reports some dead devices on the ticket (<c>DeviceNotRegistered</c>) and the
/// rest only on the receipt, fetched about 15 minutes later. Polling receipts needs a delayed job and a table of
/// pending ticket ids — delivery-platform infrastructure this module deliberately does not carry — so a device whose
/// death shows only on the receipt stays registered until the device store's own bounds remove it.
/// </para>
/// <para>
/// A failed HTTP exchange or a request-level error marks the whole batch <see cref="PushSendStatus.Failed"/> and is
/// not retried; a network failure throws.
/// </para>
/// </remarks>
[ExposeServices(typeof(IPushProvider), typeof(ExpoPushProvider))]
public class ExpoPushProvider : IPushProvider, ITransientDependency
{
    public const string ProviderName = "Expo";

    /// <summary>The <see cref="IHttpClientFactory"/> client name this provider sends through.</summary>
    public const string HttpClientName = "Dignite.Abp.Notifications.Push.Expo";

    /// <summary>Expo accepts at most this many messages per request.</summary>
    public const int MaxMessagesPerRequest = 100;

    public const string DeviceNotRegisteredError = "DeviceNotRegistered";

    private const string SendPath = "/--/api/v2/push/send";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string Name => ProviderName;

    protected IHttpClientFactory HttpClientFactory { get; }

    protected ExpoPushOptions Options { get; }

    public ExpoPushProvider(IHttpClientFactory httpClientFactory, IOptions<ExpoPushOptions> options)
    {
        HttpClientFactory = httpClientFactory;
        Options = options.Value;
    }

    public virtual async Task<IReadOnlyList<PushSendResult>> SendAsync(
        IReadOnlyList<PushMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var results = new List<PushSendResult>(messages.Count);
        for (var offset = 0; offset < messages.Count; offset += MaxMessagesPerRequest)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunk = messages.Skip(offset).Take(MaxMessagesPerRequest).ToList();
            results.AddRange(await SendChunkAsync(chunk, cancellationToken));
        }

        return results;
    }

    protected virtual async Task<IReadOnlyList<PushSendResult>> SendChunkAsync(
        IReadOnlyList<PushMessage> chunk,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(Options.BaseAddress), SendPath))
        {
            Content = JsonContent.Create(chunk.Select(ToWireMessage).ToList(), options: SerializerOptions)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(Options.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Options.AccessToken);
        }

        var client = HttpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, cancellationToken);

        ExpoPushResponse? body = null;
        try
        {
            body = await response.Content.ReadFromJsonAsync<ExpoPushResponse>(SerializerOptions, cancellationToken);
        }
        catch (JsonException)
        {
            // An HTML error page from a proxy, say. Reported below by status code.
        }

        if (!response.IsSuccessStatusCode || body?.Errors is { Count: > 0 })
        {
            var code = body?.Errors?.FirstOrDefault()?.Code;
            return FailAll(chunk, $"HTTP {(int)response.StatusCode}{(code == null ? string.Empty : " " + code)}");
        }

        if (body?.Data == null || body.Data.Count != chunk.Count)
        {
            // Tickets are matched to messages by position; without one ticket per message nothing can be attributed.
            return FailAll(chunk, "UnexpectedTicketCount");
        }

        return chunk.Select((message, index) => ToResult(message, body.Data[index])).ToList();
    }

    private static PushSendResult ToResult(PushMessage message, ExpoPushTicket ticket)
    {
        if (string.Equals(ticket.Status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            return PushSendResult.Succeeded(message.Token);
        }

        var error = ticket.Details?.Error;
        return string.Equals(error, DeviceNotRegisteredError, StringComparison.Ordinal)
            ? PushSendResult.TokenInvalid(message.Token, error)
            : PushSendResult.Failed(message.Token, error ?? "Unknown");
    }

    private ExpoPushRequestMessage ToWireMessage(PushMessage message)
    {
        return new ExpoPushRequestMessage
        {
            To = message.Token,
            Title = message.Title,
            Body = message.Body,
            Data = message.Data.Count == 0 ? null : message.Data,
            Sound = Options.Sound,
            Priority = Options.Priority,
            ChannelId = Options.AndroidChannelId
        };
    }

    private static IReadOnlyList<PushSendResult> FailAll(IReadOnlyList<PushMessage> chunk, string error)
    {
        return chunk.Select(message => PushSendResult.Failed(message.Token, error)).ToList();
    }
}
