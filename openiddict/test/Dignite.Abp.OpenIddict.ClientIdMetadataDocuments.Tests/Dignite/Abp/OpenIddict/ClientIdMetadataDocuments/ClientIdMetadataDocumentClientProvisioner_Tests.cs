using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Xunit;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Dignite.Abp.OpenIddict.ClientIdMetadataDocuments;

/// <summary>Registration in OpenIddict's real application store (EF Core on SQLite).</summary>
public class ClientIdMetadataDocumentClientProvisioner_Tests : ClientIdMetadataDocumentsIntegratedTestBase
{
    [Fact]
    public async Task Should_Register_A_Public_Pkce_Client_From_The_Document()
    {
        Fetcher.SetDocument(ClientId, Document(), TimeSpan.FromHours(2));

        (await EnsureAsync(ClientId)).ShouldBeNull();

        var client = (await FindClientAsync())!;
        client.ClientType.ShouldBe(ClientTypes.Public);
        client.ApplicationType.ShouldBe(ApplicationTypes.Web);
        client.RedirectUris.ShouldBe(new[] { "https://client.example.com/callback" });
        client.Requirements.ShouldBe(new[] { Requirements.Features.ProofKeyForCodeExchange });
        client.Permissions.ShouldBe(new[]
        {
            Permissions.Endpoints.Authorization,
            Permissions.Endpoints.Token,
            Permissions.GrantTypes.AuthorizationCode,
            Permissions.GrantTypes.RefreshToken,
            Permissions.ResponseTypes.Code,
            Permissions.Prefixes.Scope + ClientIdMetadataDocumentsTestModule.ApiScope,
            Permissions.Prefixes.Scope + "profile",
            Permissions.Prefixes.Resource + ClientIdMetadataDocumentsTestModule.Resource,
            Permissions.Prefixes.Resource + ClientIdMetadataDocumentsTestModule.Resource + "/"
        }, ignoreOrder: true);
        client.ExpiresAt.ShouldBeInRange(DateTimeOffset.UtcNow.AddHours(2).AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(2));
    }

    [Fact]
    public async Task Should_Grant_Both_Slash_Forms_Of_Every_Registered_Resource()
    {
        var resources = GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>().CurrentValue.Resources;
        resources.Add(new Uri("https://api.example.com/site-mcp/"));
        resources.Add(new Uri("https://api.example.com/"));
        Fetcher.SetDocument(ClientId, Document());

        await EnsureAsync(ClientId);

        var permissions = (await FindClientAsync())!.Permissions;
        permissions.ShouldContain("rsrc:https://api.example.com/site-mcp/");
        permissions.ShouldContain("rsrc:https://api.example.com/site-mcp");
        permissions.ShouldContain("rsrc:https://api.example.com/");
        permissions.ShouldContain("rsrc:https://api.example.com");
    }

    [Fact]
    public async Task Should_Show_The_Redirect_Hosts_In_The_Display_Name()
    {
        Fetcher.SetDocument(ClientId, Document(
            name: "Claude Code",
            redirectUris: "\"http://localhost/callback\",\"http://127.0.0.1/callback\",\"http://localhost/other\""));

        await EnsureAsync(ClientId);

        var client = (await FindClientAsync())!;
        client.DisplayName.ShouldBe("Claude Code (localhost, 127.0.0.1)");
        client.ApplicationType.ShouldBe(ApplicationTypes.Native);
    }

    [Fact]
    public async Task Should_Use_An_Overridden_Display_Name_That_Keeps_The_Hosts()
    {
        Fetcher.SetDocument(ClientId, Document());
        await EnsureAsync(ClientId, services => new LocalizedProvisioner(services.GetRequiredService<IAbpLazyServiceProvider>()));

        (await FindClientAsync())!.DisplayName.ShouldBe("Example（client.example.com）");
    }

    [Fact]
    public async Task Should_Refuse_An_Overridden_Display_Name_Without_The_Hosts()
    {
        Fetcher.SetDocument(ClientId, Document());
        var exception = await Should.ThrowAsync<AbpException>(() =>
            EnsureAsync(ClientId, services => new NameOnlyProvisioner(services.GetRequiredService<IAbpLazyServiceProvider>())));

        exception.Message.ShouldContain("client.example.com");
        (await FindClientAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Not_Fetch_Again_Within_The_Cache_Lifetime()
    {
        Fetcher.SetDocument(ClientId, Document());

        await EnsureAsync(ClientId);
        await EnsureAsync(ClientId);

        Fetcher.FetchCount.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Fetch_Again_And_Follow_The_Document_Once_Expired()
    {
        Fetcher.SetDocument(ClientId, Document());
        await EnsureAsync(ClientId);
        await SetExpiresAtAsync(DateTimeOffset.UtcNow.AddMinutes(-1));
        Fetcher.SetDocument(ClientId, Document(name: "Renamed", redirectUris: "\"https://new.example.com/callback\""));

        (await EnsureAsync(ClientId)).ShouldBeNull();

        Fetcher.FetchCount.ShouldBe(2);
        var client = (await FindClientAsync())!;
        client.DisplayName.ShouldBe("Renamed (new.example.com)");
        client.RedirectUris.ShouldBe(new[] { "https://new.example.com/callback" });
        client.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Should_Keep_An_Expired_Registration_While_The_Document_Is_Unavailable()
    {
        Fetcher.SetDocument(ClientId, Document());
        await EnsureAsync(ClientId);
        await SetExpiresAtAsync(DateTimeOffset.UtcNow.AddMinutes(-1));
        Fetcher.SetUnavailable(ClientId);

        (await EnsureAsync(ClientId)).ShouldBeNull();

        (await FindClientAsync()).ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_Refuse_An_Expired_Registration_Whose_Document_Became_Invalid()
    {
        Fetcher.SetDocument(ClientId, Document());
        await EnsureAsync(ClientId);
        await SetExpiresAtAsync(DateTimeOffset.UtcNow.AddMinutes(-1));
        Fetcher.SetInvalid(ClientId, "The client metadata document returned HTTP 404.");

        (await EnsureAsync(ClientId)).ShouldBe("The client metadata document returned HTTP 404.");
    }

    [Fact]
    public async Task Should_Refuse_A_Client_Whose_Document_Cannot_Be_Fetched()
    {
        Fetcher.SetUnavailable(ClientId);

        (await EnsureAsync(ClientId)).ShouldNotBeNull();

        (await FindClientAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Apply_A_Resource_Added_To_The_Server_Before_The_Registration_Expires()
    {
        Fetcher.SetDocument(ClientId, Document());
        await EnsureAsync(ClientId);
        GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>().CurrentValue.Resources
            .Add(new Uri("https://api.example.com/vault-mcp"));

        await EnsureAsync(ClientId);

        Fetcher.FetchCount.ShouldBe(1);
        (await FindClientAsync())!.Permissions.ShouldContain("rsrc:https://api.example.com/vault-mcp/");
    }

    [Fact]
    public async Task Should_Leave_A_Preregistered_Https_Client_Alone()
    {
        const string preregistered = "https://partner.example.com/client";
        await CreateApplicationAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = preregistered,
            ClientType = ClientTypes.Public,
            DisplayName = "Partner",
            RedirectUris = { new Uri("https://partner.example.com/callback") },
            Permissions = { Permissions.Endpoints.Authorization }
        });

        (await EnsureAsync(preregistered)).ShouldBeNull();

        Fetcher.FetchCount.ShouldBe(0);
        var client = (await FindClientAsync(preregistered))!;
        client.DisplayName.ShouldBe("Partner");
        client.Permissions.ShouldBe(new[] { Permissions.Endpoints.Authorization });
    }

    [Fact]
    public async Task Should_Refuse_A_Client_Id_Longer_Than_The_Store_Allows()
    {
        var longClientId = "https://client.example.com/" + new string('a', 100);

        (await EnsureAsync(longClientId)).ShouldNotBeNull();

        Fetcher.FetchCount.ShouldBe(0);
    }

    [Fact]
    public async Task Should_Register_In_The_Host_When_A_Tenant_Is_Resolved()
    {
        Fetcher.SetDocument(ClientId, Document());

        using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
        {
            (await EnsureAsync(ClientId)).ShouldBeNull();
        }

        (await FindClientAsync()).ShouldNotBeNull();
    }

    private sealed class LocalizedProvisioner(IAbpLazyServiceProvider lazyServiceProvider)
        : ClientIdMetadataDocumentClientProvisioner(lazyServiceProvider)
    {
        protected override string GetDisplayName(string clientName, IReadOnlyList<string> redirectHosts) =>
            $"{clientName}（{string.Join("、", redirectHosts)}）";
    }

    private sealed class NameOnlyProvisioner(IAbpLazyServiceProvider lazyServiceProvider)
        : ClientIdMetadataDocumentClientProvisioner(lazyServiceProvider)
    {
        protected override string GetDisplayName(string clientName, IReadOnlyList<string> redirectHosts) => clientName;
    }
}
