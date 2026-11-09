using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using OpenIddict.Server;
using Shouldly;
using Xunit;
using static OpenIddict.Server.OpenIddictServerHandlers;

namespace Dignite.Abp.OpenIddict.ClientIdMetadataDocuments;

/// <summary>
/// The two handlers inside OpenIddict's real pipeline. Both depend on where they sit among OpenIddict's own handlers,
/// so these tests check the order directly and through what the endpoints answer.
/// </summary>
public class ClientIdMetadataDocumentServerPipeline_Tests : ClientIdMetadataDocumentsIntegratedTestBase
{
    [Fact]
    public void Provisioning_Should_Run_After_The_Client_Id_Is_Required_And_Before_The_Client_Is_Looked_Up()
    {
        var order = ProvisionClientIdMetadataDocumentClient.Descriptor.Order;

        order.ShouldBeGreaterThan(Authentication.ValidateClientIdParameter.Descriptor.Order);
        order.ShouldBeLessThan(Authentication.ValidateAuthentication.Descriptor.Order);
    }

    [Fact]
    public void Discovery_Should_Run_After_OpenIddict_Fills_The_Token_Endpoint_Auth_Methods()
    {
        var order = AttachClientIdMetadataDocumentSupport.Descriptor.Order;

        order.ShouldBeGreaterThan(Discovery.AttachClientAuthenticationMethods.Descriptor.Order);
        order.ShouldBeLessThan(Discovery.AttachCodeChallengeMethods.Descriptor.Order);
    }

    [Fact]
    public void Both_Handlers_Should_Be_Registered_By_The_Module()
    {
        var handlers = GetRequiredService<IOptions<OpenIddictServerOptions>>().Value.Handlers;

        handlers.ShouldContain(ProvisionClientIdMetadataDocumentClient.Descriptor);
        handlers.ShouldContain(AttachClientIdMetadataDocumentSupport.Descriptor);
    }

    [Fact]
    public async Task Discovery_Should_Advertise_Cimd_With_None_And_Without_Private_Key_Jwt()
    {
        using var document = await GetJsonAsync("/.well-known/openid-configuration");
        var root = document.RootElement;

        root.GetProperty(AttachClientIdMetadataDocumentSupport.MetadataName).GetBoolean().ShouldBeTrue();
        var methods = root.GetProperty("token_endpoint_auth_methods_supported").EnumerateArray().Select(e => e.GetString()).ToList();
        methods.ShouldContain("none");
        methods.ShouldContain("client_secret_post");
        methods.ShouldNotContain("private_key_jwt");
    }

    [Fact]
    public async Task Authorization_Request_Should_Register_The_Client_And_Pass_OpenIddict_Validation()
    {
        Fetcher.SetDocument(ClientId, Document());

        // The trailing-slash form of the registered resource: what cloud's clients send, and what failed as ID2192
        // without the rsrc: permissions the provisioner grants.
        var response = await AuthorizeAsync(ClientId, ClientIdMetadataDocumentsTestModule.Resource + "/");

        // OpenIddict passed the request on to ABP's authorization endpoint, which asks the user to sign in.
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        location.ShouldNotStartWith("https://client.example.com");
        location.ShouldNotContain("error=");
        (await FindClientAsync()).ShouldNotBeNull();
    }

    [Fact]
    public async Task Authorization_Request_Should_Be_Refused_As_Invalid_Client_When_The_Document_Is_Invalid()
    {
        Fetcher.SetDocument(ClientId, Document(clientId: "https://other.example.com/metadata.json"));

        var response = await AuthorizeAsync(ClientId, ClientIdMetadataDocumentsTestModule.Resource);

        // OpenIddict cannot send the error to a redirect URI it has not validated, so it answers itself.
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("invalid_client");
        body.ShouldContain("does not match the document URL");
        (await FindClientAsync()).ShouldBeNull();
    }

    private async Task<JsonDocument> GetJsonAsync(string path)
    {
        var response = await Client.GetAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private Task<HttpResponseMessage> AuthorizeAsync(string clientId, string resource)
    {
        var verifier = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var challenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var query = string.Join("&", new[]
        {
            "response_type=code",
            "client_id=" + Uri.EscapeDataString(clientId),
            "redirect_uri=" + Uri.EscapeDataString("https://client.example.com/callback"),
            "scope=" + Uri.EscapeDataString("openid " + ClientIdMetadataDocumentsTestModule.ApiScope),
            "code_challenge=" + challenge,
            "code_challenge_method=S256",
            "state=xyz",
            "resource=" + Uri.EscapeDataString(resource)
        });

        return Client.GetAsync("/connect/authorize?" + query);
    }
}
