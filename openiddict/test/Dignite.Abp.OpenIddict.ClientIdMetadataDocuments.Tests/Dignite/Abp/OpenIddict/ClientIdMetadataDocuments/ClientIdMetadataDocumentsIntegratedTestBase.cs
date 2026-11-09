using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.Uow;

namespace Dignite.Abp.OpenIddict.ClientIdMetadataDocuments;

public abstract class ClientIdMetadataDocumentsIntegratedTestBase : AbpWebApplicationFactoryIntegratedTest<Program>
{
    protected const string ClientId = "https://client.example.com/oauth/metadata.json";

    internal StubClientIdMetadataDocumentFetcher Fetcher => GetRequiredService<StubClientIdMetadataDocumentFetcher>();

    protected static string Document(
        string clientId = ClientId,
        string name = "Example",
        string redirectUris = "\"https://client.example.com/callback\"",
        string extra = ",\"grant_types\":[\"authorization_code\",\"refresh_token\"]") =>
        $$"""{"client_id":"{{clientId}}","client_name":"{{name}}","redirect_uris":[{{redirectUris}}]{{extra}}}""";

    /// <summary>
    /// Runs the provisioner in a scope of its own, as each authorization request does (the application manager is
    /// scoped).
    /// </summary>
    protected async Task<string?> EnsureAsync(
        string clientId,
        Func<IServiceProvider, ClientIdMetadataDocumentClientProvisioner>? createProvisioner = null)
    {
        using var scope = ServiceProvider.CreateScope();
        var provisioner = createProvisioner?.Invoke(scope.ServiceProvider)
                          ?? scope.ServiceProvider.GetRequiredService<ClientIdMetadataDocumentClientProvisioner>();
        return await provisioner.EnsureAsync(clientId);
    }

    protected async Task<T> WithUnitOfWorkAsync<T>(Func<IOpenIddictApplicationManager, Task<T>> action)
    {
        using var scope = ServiceProvider.CreateScope();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        using var uow = uowManager.Begin(requiresNew: true);
        var result = await action(scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>());
        await uow.CompleteAsync();
        return result;
    }

    protected Task<RegisteredClient?> FindClientAsync(string clientId = ClientId) =>
        WithUnitOfWorkAsync(async manager =>
        {
            var application = await manager.FindByClientIdAsync(clientId);
            if (application == null)
            {
                return null;
            }

            return new RegisteredClient(
                await manager.GetIdAsync(application) ?? string.Empty,
                await manager.GetDisplayNameAsync(application),
                await manager.GetApplicationTypeAsync(application),
                await manager.GetClientTypeAsync(application),
                (await manager.GetPermissionsAsync(application)).ToList(),
                (await manager.GetRedirectUrisAsync(application)).ToList(),
                (await manager.GetRequirementsAsync(application)).ToList(),
                await manager.GetPropertiesAsync(application));
        });

    protected Task CreateApplicationAsync(OpenIddictApplicationDescriptor descriptor) =>
        WithUnitOfWorkAsync(async manager =>
        {
            await manager.CreateAsync(descriptor);
            return true;
        });

    protected Task SetExpiresAtAsync(DateTimeOffset expiresAt, string clientId = ClientId) =>
        WithUnitOfWorkAsync(async manager =>
        {
            var application = (await manager.FindByClientIdAsync(clientId))!;
            var descriptor = new OpenIddictApplicationDescriptor();
            await manager.PopulateAsync(descriptor, application);
            descriptor.Properties[ClientIdMetadataDocumentRegistration.ExpiresAtProperty] =
                ClientIdMetadataDocumentRegistration.CreateExpiresAt(expiresAt);
            await manager.UpdateAsync(application, descriptor);
            return true;
        });

    protected sealed record RegisteredClient(
        string Id,
        string? DisplayName,
        string? ApplicationType,
        string? ClientType,
        IReadOnlyList<string> Permissions,
        IReadOnlyList<string> RedirectUris,
        IReadOnlyList<string> Requirements,
        ImmutableDictionary<string, JsonElement> Properties)
    {
        public DateTimeOffset ExpiresAt =>
            ClientIdMetadataDocumentRegistration.TryGetExpiresAt(Properties, out var expiresAt) ? expiresAt : default;
    }
}
