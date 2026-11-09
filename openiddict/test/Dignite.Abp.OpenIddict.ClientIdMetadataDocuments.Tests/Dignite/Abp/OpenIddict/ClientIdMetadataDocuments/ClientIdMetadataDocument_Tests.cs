using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace Dignite.Abp.OpenIddict.ClientIdMetadataDocuments;

/// <summary>The client_id URL rules, document validation, the SSRF guards of the fetch, and the cache lifetime. Ported from shiji.</summary>
public class ClientIdMetadataDocument_Tests
{
    private const string ClientId = "https://client.example.com/oauth/metadata.json";

    private static string Doc(string? clientId = ClientId, string extra = "") =>
        $$"""{"client_id":"{{clientId}}","client_name":"Example","redirect_uris":["https://client.example.com/callback","http://localhost/callback"]{{extra}}}""";

    [Theory]
    [InlineData("https://claude.ai/oauth/claude-code-client-metadata")]
    [InlineData("https://chatgpt.com/oauth/client.json?v=2")]
    public void Should_Accept_Https_Client_Id_With_Path(string clientId)
    {
        ClientIdMetadataDocumentRules.IsClientIdUrl(clientId).ShouldBeTrue();
        ClientIdMetadataDocumentRules.ParseClientIdUrl(clientId).Host.ShouldNotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("http://client.example.com/metadata.json")]
    [InlineData("https://client.example.com")]
    [InlineData("https://client.example.com/")]
    [InlineData("https://client.example.com/a/../metadata.json")]
    [InlineData("https://client.example.com/./metadata.json")]
    [InlineData("https://client.example.com/metadata.json#x")]
    [InlineData("https://user:pass@client.example.com/metadata.json")]
    public void Should_Reject_Invalid_Client_Id_Url(string clientId)
    {
        Should.Throw<ClientIdMetadataDocumentException>(() => ClientIdMetadataDocumentRules.ParseClientIdUrl(clientId));
    }

    [Fact]
    public void Should_Leave_Plain_Client_Ids_To_The_Client_Table()
    {
        ClientIdMetadataDocumentRules.IsClientIdUrl("MyApp_Mcp").ShouldBeFalse();
        ClientIdMetadataDocumentRules.IsClientIdUrl(null).ShouldBeFalse();
    }

    [Fact]
    public void Should_Parse_Valid_Document_With_Defaults()
    {
        var document = ClientIdMetadataDocumentRules.Parse(ClientId, Encoding.UTF8.GetBytes(Doc()));

        document.ClientName.ShouldBe("Example");
        document.RedirectUris.Count.ShouldBe(2);
        document.GrantTypes.ShouldBe(new[] { "authorization_code" });
        document.HasLoopbackRedirect.ShouldBeTrue();
    }

    [Theory]
    [InlineData("https://other.example.com/metadata.json", "")]
    [InlineData(ClientId, ",\"token_endpoint_auth_method\":\"client_secret_basic\"")]
    [InlineData(ClientId, ",\"token_endpoint_auth_method\":\"private_key_jwt\"")]
    [InlineData(ClientId, ",\"grant_types\":[\"client_credentials\"]")]
    [InlineData(ClientId, ",\"grant_types\":[\"refresh_token\"]")]
    [InlineData(ClientId, ",\"response_types\":[\"token\"]")]
    public void Should_Reject_Document_That_Breaks_The_Rules(string clientId, string extra)
    {
        Should.Throw<ClientIdMetadataDocumentException>(
            () => ClientIdMetadataDocumentRules.Parse(ClientId, Encoding.UTF8.GetBytes(Doc(clientId, extra))));
    }

    [Theory]
    [InlineData("""{"client_id":"https://client.example.com/oauth/metadata.json","redirect_uris":["https://a.example.com/cb"]}""")]
    [InlineData("""{"client_id":"https://client.example.com/oauth/metadata.json","client_name":"X","redirect_uris":[]}""")]
    [InlineData("""{"client_id":"https://client.example.com/oauth/metadata.json","client_name":"X","redirect_uris":["http://evil.example.com/cb"]}""")]
    [InlineData("""{"client_id":"https://client.example.com/oauth/metadata.json","client_name":"X","redirect_uris":["cursor://callback"]}""")]
    [InlineData("""[1,2]""")]
    [InlineData("""not json""")]
    public void Should_Reject_Malformed_Document(string json)
    {
        Should.Throw<ClientIdMetadataDocumentException>(() => ClientIdMetadataDocumentRules.Parse(ClientId, Encoding.UTF8.GetBytes(json)));
    }

    /// <summary>Fetched from https://chatgpt.com/oauth/client.json on 2026-11-06: the singular field prefers private_key_jwt, the list includes none.</summary>
    [Fact]
    public void Should_Accept_ChatGPT_Document_Because_It_Also_Supports_None()
    {
        const string chatGpt = """{"client_id":"https://chatgpt.com/oauth/client.json","client_uri":"https://chatgpt.com/","redirect_uris":["https://chatgpt.com/connector_platform_oauth_redirect"],"token_endpoint_auth_method":"private_key_jwt","token_endpoint_auth_methods_supported":["none","private_key_jwt"],"grant_types":["authorization_code","refresh_token"],"response_types":["code"],"client_name":"ChatGPT","logo_uri":"https://persistent.oaistatic.com/sonic/misc/openai-logo.png","token_endpoint_auth_signing_alg":"RS256","jwks_uri":"https://chatgpt.com/oauth/jwks.json"}""";

        var document = ClientIdMetadataDocumentRules.Parse("https://chatgpt.com/oauth/client.json", Encoding.UTF8.GetBytes(chatGpt));

        document.ClientName.ShouldBe("ChatGPT");
        document.HasLoopbackRedirect.ShouldBeFalse();
        document.GrantTypes.ShouldContain("refresh_token");
    }

    /// <summary>Fetched from https://claude.ai/oauth/claude-code-client-metadata on 2026-11-06.</summary>
    [Fact]
    public void Should_Accept_Claude_Code_Document()
    {
        const string claudeCode = """{"client_id":"https://claude.ai/oauth/claude-code-client-metadata","client_name":"Claude Code","client_uri":"https://claude.ai","redirect_uris":["http://localhost/callback","http://127.0.0.1/callback"],"grant_types":["authorization_code","refresh_token"],"response_types":["code"],"token_endpoint_auth_method":"none"}""";

        var document = ClientIdMetadataDocumentRules.Parse("https://claude.ai/oauth/claude-code-client-metadata", Encoding.UTF8.GetBytes(claudeCode));

        document.HasLoopbackRedirect.ShouldBeTrue();
    }

    /// <summary>Fetched from https://claude.ai/oauth/mcp-oauth-client-metadata on 2026-11-07: it also lists jwt-bearer; only the two supported grant types are kept.</summary>
    [Fact]
    public void Should_Accept_Claude_Document_And_Drop_Unsupported_Grant_Types()
    {
        const string claude = """{"client_id":"https://claude.ai/oauth/mcp-oauth-client-metadata","client_name":"Claude","client_uri":"https://claude.ai","redirect_uris":["https://claude.ai/api/mcp/auth_callback"],"grant_types":["authorization_code","refresh_token","urn:ietf:params:oauth:grant-type:jwt-bearer"],"response_types":["code"],"token_endpoint_auth_method":"none"}""";

        var document = ClientIdMetadataDocumentRules.Parse("https://claude.ai/oauth/mcp-oauth-client-metadata", Encoding.UTF8.GetBytes(claude));

        document.GrantTypes.ShouldBe(new[] { "authorization_code", "refresh_token" });
    }

    [Fact]
    public void Should_Reject_Document_Whose_Method_List_Lacks_None()
    {
        Should.Throw<ClientIdMetadataDocumentException>(() => ClientIdMetadataDocumentRules.Parse(ClientId,
            Encoding.UTF8.GetBytes(Doc(extra: ",\"token_endpoint_auth_method\":\"none\",\"token_endpoint_auth_methods_supported\":[\"private_key_jwt\"]"))));
    }

    [Fact]
    public void Should_Accept_Refresh_Token_Grant()
    {
        var document = ClientIdMetadataDocumentRules.Parse(ClientId,
            Encoding.UTF8.GetBytes(Doc(extra: ",\"grant_types\":[\"authorization_code\",\"refresh_token\"],\"token_endpoint_auth_method\":\"none\"")));

        document.GrantTypes.ShouldContain("refresh_token");
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.20.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("2001:db8::1")]
    public void Should_Block_Non_Public_Addresses(string address)
    {
        ClientIdMetadataDocumentFetcher.IsPublicAddress(IPAddress.Parse(address)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("160.79.104.10")]
    [InlineData("2606:4700::1111")]
    public void Should_Allow_Public_Addresses(string address)
    {
        ClientIdMetadataDocumentFetcher.IsPublicAddress(IPAddress.Parse(address)).ShouldBeTrue();
    }

    [Fact]
    public void Should_Clamp_Cache_Lifetime()
    {
        var fetcher = new ClientIdMetadataDocumentFetcher(new HttpClient(), new AbpClientIdMetadataDocumentOptions());

        fetcher.GetCacheLifetime(null).ShouldBe(TimeSpan.FromHours(1));
        fetcher.GetCacheLifetime(new CacheControlHeaderValue { MaxAge = TimeSpan.FromSeconds(10) }).ShouldBe(TimeSpan.FromMinutes(5));
        fetcher.GetCacheLifetime(new CacheControlHeaderValue { MaxAge = TimeSpan.FromDays(7) }).ShouldBe(TimeSpan.FromHours(24));
        fetcher.GetCacheLifetime(new CacheControlHeaderValue { NoStore = true }).ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task Should_Fetch_And_Validate_Document()
    {
        var fetcher = CreateFetcher(_ => Json(HttpStatusCode.OK, Doc(), maxAge: TimeSpan.FromHours(2)));

        var result = await fetcher.FetchAsync(ClientId, CancellationToken.None);

        result.Status.ShouldBe(ClientIdMetadataDocumentFetchStatus.Ok);
        result.Document!.ClientId.ShouldBe(ClientId);
        result.CacheLifetime.ShouldBe(TimeSpan.FromHours(2));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, nameof(ClientIdMetadataDocumentFetchStatus.Invalid))]
    [InlineData(HttpStatusCode.Found, nameof(ClientIdMetadataDocumentFetchStatus.Invalid))]
    [InlineData(HttpStatusCode.ServiceUnavailable, nameof(ClientIdMetadataDocumentFetchStatus.Unavailable))]
    public async Task Should_Classify_Http_Failures(HttpStatusCode status, string expected)
    {
        var fetcher = CreateFetcher(_ => new HttpResponseMessage(status));

        (await fetcher.FetchAsync(ClientId, CancellationToken.None)).Status.ToString().ShouldBe(expected);
    }

    [Fact]
    public async Task Should_Reject_Oversized_Document()
    {
        var fetcher = CreateFetcher(_ => Json(HttpStatusCode.OK, Doc(extra: $",\"x\":\"{new string('a', 6000)}\"")));

        var result = await fetcher.FetchAsync(ClientId, CancellationToken.None);

        result.Status.ShouldBe(ClientIdMetadataDocumentFetchStatus.Invalid);
    }

    [Fact]
    public async Task Should_Reject_Hosts_Outside_Allow_List()
    {
        var options = new AbpClientIdMetadataDocumentOptions { AllowedHosts = { "claude.ai" } };
        var fetcher = new ClientIdMetadataDocumentFetcher(new HttpClient(new StubHandler(_ => Json(HttpStatusCode.OK, Doc()))), options);

        (await fetcher.FetchAsync(ClientId, CancellationToken.None)).Status.ShouldBe(ClientIdMetadataDocumentFetchStatus.Invalid);
    }

    [Fact]
    public async Task Guarded_Handler_Should_Refuse_Hosts_That_Resolve_To_Loopback()
    {
        // Resolving localhost can take longer than the default 5 seconds on a busy machine running the whole suite, which
        // turns "refused" into "unavailable". Only the loopback refusal is under test here, so the timeout is relaxed.
        var options = new AbpClientIdMetadataDocumentOptions { FetchTimeout = TimeSpan.FromMinutes(1) };
        var fetcher = new ClientIdMetadataDocumentFetcher(new HttpClient(ClientIdMetadataDocumentFetcher.CreateGuardedHandler(options)), options);

        var result = await fetcher.FetchAsync("https://localhost/metadata.json", CancellationToken.None);

        result.Status.ShouldBe(ClientIdMetadataDocumentFetchStatus.Invalid);
        result.Error.ShouldContain("public address");
    }

    private static ClientIdMetadataDocumentFetcher CreateFetcher(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new HttpClient(new StubHandler(respond)), new AbpClientIdMetadataDocumentOptions());

    private static HttpResponseMessage Json(HttpStatusCode status, string body, TimeSpan? maxAge = null)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (maxAge != null)
        {
            response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = maxAge };
        }

        return response;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
