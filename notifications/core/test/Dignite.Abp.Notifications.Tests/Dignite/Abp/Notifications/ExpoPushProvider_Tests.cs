using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.Notifications.Push;
using Dignite.Abp.Notifications.Push.Expo;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Dignite.Abp.Notifications;

public class ExpoPushProvider_Tests
{
    [Fact]
    public async Task Posts_the_messages_to_the_expo_send_endpoint_with_the_access_token()
    {
        var handler = new FakeExpoHandler();
        var provider = CreateProvider(handler, new ExpoPushOptions { AccessToken = "secret", AndroidChannelId = "default" });

        await provider.SendAsync(new[] { Message("ExponentPushToken[a]", title: "Title") });

        var request = handler.Requests.Single();
        request.Uri.ShouldBe(new Uri("https://exp.host/--/api/v2/push/send"));
        request.Authorization.ShouldBe("Bearer secret");
        var sent = request.Body.RootElement.EnumerateArray().Single();
        sent.GetProperty("to").GetString().ShouldBe("ExponentPushToken[a]");
        sent.GetProperty("title").GetString().ShouldBe("Title");
        sent.GetProperty("body").GetString().ShouldBe("Body");
        sent.GetProperty("data").GetProperty("notificationId").GetString().ShouldBe("n1");
        sent.GetProperty("sound").GetString().ShouldBe("default");
        sent.GetProperty("priority").GetString().ShouldBe("high");
        sent.GetProperty("channelId").GetString().ShouldBe("default");
    }

    [Fact]
    public async Task Sends_no_authorization_header_and_no_null_fields_without_configuration()
    {
        var handler = new FakeExpoHandler();
        var provider = CreateProvider(handler, new ExpoPushOptions());

        await provider.SendAsync(new[] { Message("ExponentPushToken[a]") });

        var request = handler.Requests.Single();
        request.Authorization.ShouldBeNull();
        var sent = request.Body.RootElement.EnumerateArray().Single();
        sent.TryGetProperty("title", out _).ShouldBeFalse();
        sent.TryGetProperty("channelId", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Splits_more_than_one_hundred_messages_into_several_requests()
    {
        var handler = new FakeExpoHandler();
        var provider = CreateProvider(handler, new ExpoPushOptions());
        var messages = Enumerable.Range(0, 150).Select(index => Message($"ExponentPushToken[{index}]")).ToList();

        var results = await provider.SendAsync(messages);

        handler.Requests.Select(request => request.Body.RootElement.GetArrayLength()).ShouldBe(new[] { 100, 50 });
        results.Select(result => result.Token).ShouldBe(messages.Select(message => message.Token));
        results.ShouldAllBe(result => result.Status == PushSendStatus.Succeeded);
    }

    [Fact]
    public async Task Maps_each_ticket_to_its_message_by_position()
    {
        var handler = new FakeExpoHandler
        {
            Respond = _ => Json(HttpStatusCode.OK, """
                {"data":[
                  {"status":"ok","id":"1"},
                  {"status":"error","message":"gone","details":{"error":"DeviceNotRegistered"}},
                  {"status":"error","message":"too big","details":{"error":"MessageTooBig"}},
                  {"status":"error","message":"what"}
                ]}
                """)
        };
        var provider = CreateProvider(handler, new ExpoPushOptions());

        var results = await provider.SendAsync(new[] { Message("a"), Message("b"), Message("c"), Message("d") });

        results.Select(result => (result.Token, result.Status, result.Error)).ShouldBe(new (string, PushSendStatus, string?)[]
        {
            ("a", PushSendStatus.Succeeded, null),
            ("b", PushSendStatus.TokenInvalid, "DeviceNotRegistered"),
            ("c", PushSendStatus.Failed, "MessageTooBig"),
            ("d", PushSendStatus.Failed, "Unknown")
        });
    }

    [Fact]
    public async Task Fails_the_whole_batch_on_a_request_level_error()
    {
        var handler = new FakeExpoHandler
        {
            Respond = _ => Json(
                HttpStatusCode.TooManyRequests,
                """{"errors":[{"code":"TOO_MANY_REQUESTS","message":"slow down"}]}""")
        };
        var provider = CreateProvider(handler, new ExpoPushOptions());

        var results = await provider.SendAsync(new[] { Message("a"), Message("b") });

        results.ShouldAllBe(result =>
            result.Status == PushSendStatus.Failed && result.Error == "HTTP 429 TOO_MANY_REQUESTS");
    }

    [Fact]
    public async Task Fails_the_whole_batch_on_a_non_json_error_page()
    {
        var handler = new FakeExpoHandler
        {
            Respond = _ => new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("<html>bad gateway</html>", Encoding.UTF8, "text/html")
            }
        };
        var provider = CreateProvider(handler, new ExpoPushOptions());

        var results = await provider.SendAsync(new[] { Message("a") });

        results.Single().Error.ShouldBe("HTTP 502");
    }

    [Fact]
    public async Task Fails_the_whole_batch_when_tickets_do_not_line_up_with_messages()
    {
        var handler = new FakeExpoHandler
        {
            Respond = _ => Json(HttpStatusCode.OK, """{"data":[{"status":"ok","id":"1"}]}""")
        };
        var provider = CreateProvider(handler, new ExpoPushOptions());

        var results = await provider.SendAsync(new[] { Message("a"), Message("b") });

        results.ShouldAllBe(result => result.Status == PushSendStatus.Failed && result.Error == "UnexpectedTicketCount");
    }

    private static ExpoPushProvider CreateProvider(FakeExpoHandler handler, ExpoPushOptions options)
    {
        return new ExpoPushProvider(new FakeHttpClientFactory(handler), Options.Create(options));
    }

    private static PushMessage Message(string token, string? title = null)
    {
        return new PushMessage(
            token,
            title,
            "Body",
            new Dictionary<string, string> { [PushDataKeys.NotificationId] = "n1" });
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed record RecordedRequest(Uri Uri, string? Authorization, JsonDocument Body);

    private sealed class FakeExpoHandler : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = new();

        /// <summary>Defaults to one "ok" ticket per message sent.</summary>
        public Func<JsonDocument, HttpResponseMessage>? Respond { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Requests.Add(new RecordedRequest(request.RequestUri!, request.Headers.Authorization?.ToString(), body));

            if (Respond != null)
            {
                return Respond(body);
            }

            var tickets = string.Join(",", Enumerable.Repeat("""{"status":"ok","id":"x"}""", body.RootElement.GetArrayLength()));
            return Json(HttpStatusCode.OK, $$"""{"data":[{{tickets}}]}""");
        }
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public FakeHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
        {
            name.ShouldBe(ExpoPushProvider.HttpClientName);
            return new HttpClient(_handler, disposeHandler: false);
        }
    }
}
