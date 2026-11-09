using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.OpenIddict.Applications;
using Volo.Abp.Uow;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Dignite.Abp.OpenIddict.ClientIdMetadataDocuments;

/// <summary>
/// Registers a CIMD client in OpenIddict's application store, so redirect URI validation, PKCE, consent, the code
/// exchange and refresh all run OpenIddict's own flow. The registration comes only from the fetched document and is
/// fetched again once its cache lifetime has passed, following any change. This is not dynamic client registration
/// (RFC 7591): there is no registration endpoint and a client cannot assert anything; the server fetches the
/// document from the client's own URL.
/// </summary>
public class ClientIdMetadataDocumentClientProvisioner : ITransientDependency
{
    protected IAbpLazyServiceProvider LazyServiceProvider { get; }

    protected IOpenIddictApplicationManager ApplicationManager => LazyServiceProvider.LazyGetRequiredService<IOpenIddictApplicationManager>();

    protected IUnitOfWorkManager UnitOfWorkManager => LazyServiceProvider.LazyGetRequiredService<IUnitOfWorkManager>();

    protected ICurrentTenant CurrentTenant => LazyServiceProvider.LazyGetRequiredService<ICurrentTenant>();

    protected AbpClientIdMetadataDocumentOptions Options =>
        LazyServiceProvider.LazyGetRequiredService<IOptions<AbpClientIdMetadataDocumentOptions>>().Value;

    protected ILogger Logger =>
        LazyServiceProvider.LazyGetService<ILogger<ClientIdMetadataDocumentClientProvisioner>>(NullLogger<ClientIdMetadataDocumentClientProvisioner>.Instance);

    private ClientIdMetadataDocumentFetcher Fetcher => LazyServiceProvider.LazyGetRequiredService<ClientIdMetadataDocumentFetcher>();

    private OpenIddictServerOptions ServerOptions =>
        LazyServiceProvider.LazyGetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>().CurrentValue;

    public ClientIdMetadataDocumentClientProvisioner(IAbpLazyServiceProvider lazyServiceProvider)
    {
        LazyServiceProvider = lazyServiceProvider;
    }

    /// <summary>
    /// Makes sure the client at <paramref name="clientId"/> is registered and current.
    /// </summary>
    /// <returns><see langword="null"/> when the client can proceed; otherwise why it was refused, which is returned
    /// to the client as the <c>error_description</c>.</returns>
    public virtual async Task<string?> EnsureAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (clientId.Length > OpenIddictApplicationConsts.ClientIdMaxLength)
        {
            return $"client_id is longer than the {OpenIddictApplicationConsts.ClientIdMaxLength} characters this server can register.";
        }

        // OpenIddict's tables belong to the host: ABP's OpenIddictApplication is not IMultiTenant and its DbContext is
        // [IgnoreMultiTenancy]. The authorization request may already have resolved a tenant; the registration is not
        // that tenant's.
        using (CurrentTenant.Change(null))
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: false);

            var hostPermissions = ClientIdMetadataDocumentRegistration.GetHostPermissions(Options.Scopes, ServerOptions.Resources);

            var application = await ApplicationManager.FindByClientIdAsync(clientId, cancellationToken);
            if (application != null)
            {
                var properties = await ApplicationManager.GetPropertiesAsync(application, cancellationToken);
                if (!ClientIdMetadataDocumentRegistration.TryGetExpiresAt(properties, out var expiresAt))
                {
                    // Registered some other way (pre-registered with an https client_id): not ours to change.
                    await uow.CompleteAsync(cancellationToken);
                    return null;
                }

                if (expiresAt > DateTimeOffset.UtcNow)
                {
                    await UpdateHostPermissionsIfChangedAsync(application, hostPermissions, cancellationToken);
                    await uow.CompleteAsync(cancellationToken);
                    return null;
                }
            }

            var result = await Fetcher.FetchAsync(clientId, cancellationToken);
            switch (result.Status)
            {
                case ClientIdMetadataDocumentFetchStatus.Unavailable when application != null:
                    // A client whose document host is briefly down keeps signing in; the next request fetches again.
                    Logger.LogWarning("Client metadata document for {ClientId} unavailable ({Error}); using the existing registration.",
                        clientId, result.Error);
                    await UpdateHostPermissionsIfChangedAsync(application, hostPermissions, cancellationToken);
                    await uow.CompleteAsync(cancellationToken);
                    return null;

                case ClientIdMetadataDocumentFetchStatus.Unavailable:
                case ClientIdMetadataDocumentFetchStatus.Invalid:
                    Logger.LogWarning("CIMD client {ClientId} refused: {Error}", clientId, result.Error);
                    return result.Error;
            }

            var document = result.Document!;
            var descriptor = new OpenIddictApplicationDescriptor();
            if (application != null)
            {
                await ApplicationManager.PopulateAsync(descriptor, application, cancellationToken);
            }

            Fill(descriptor, document, DateTimeOffset.UtcNow + result.CacheLifetime, hostPermissions);

            if (application == null)
            {
                try
                {
                    await ApplicationManager.CreateAsync(descriptor, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Two authorization requests of one client arriving together: the other one may have created it.
                    if (await ApplicationManager.FindByClientIdAsync(clientId, cancellationToken) == null)
                    {
                        throw;
                    }
                }
            }
            else
            {
                await ApplicationManager.UpdateAsync(application, descriptor, cancellationToken);
            }

            await uow.CompleteAsync(cancellationToken);
            return null;
        }
    }

    /// <summary>
    /// The application's display name, shown on the consent page. The client name alone can be anything the client
    /// likes; the redirect hosts are where the authorization code actually goes, and the specification requires the
    /// consent page to show them. An override may change the wording or the language, but must keep every host in
    /// <paramref name="redirectHosts"/>: a name without one of them is refused.
    /// </summary>
    /// <param name="clientName">The document's <c>client_name</c>, trimmed.</param>
    /// <param name="redirectHosts">The distinct IDN hosts of the document's redirect URIs, in document order.</param>
    protected virtual string GetDisplayName(string clientName, IReadOnlyList<string> redirectHosts)
    {
        return $"{clientName} ({string.Join(", ", redirectHosts)})";
    }

    private void Fill(
        OpenIddictApplicationDescriptor descriptor,
        ClientIdMetadataDocument document,
        DateTimeOffset expiresAt,
        IReadOnlySet<string> hostPermissions)
    {
        descriptor.ClientId = document.ClientId;
        descriptor.ClientSecret = null;
        descriptor.ClientType = ClientTypes.Public;
        descriptor.ApplicationType = document.HasLoopbackRedirect ? ApplicationTypes.Native : ApplicationTypes.Web;
        descriptor.ConsentType = ConsentTypes.Explicit;

        var redirectHosts = document.RedirectUris
            .Select(uri => uri.IdnHost)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var displayName = GetDisplayName(document.ClientName, redirectHosts);
        if (string.IsNullOrWhiteSpace(displayName)
            || redirectHosts.Any(host => !displayName.Contains(host, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AbpException(
                $"{GetType().FullName}.{nameof(GetDisplayName)} returned '{displayName}', which does not show every redirect host " +
                $"({string.Join(", ", redirectHosts)}). The consent page must show where the authorization code is sent.");
        }

        descriptor.DisplayName = displayName;

        descriptor.RedirectUris.Clear();
        foreach (var uri in document.RedirectUris)
        {
            descriptor.RedirectUris.Add(uri);
        }

        descriptor.Permissions.Clear();
        descriptor.Permissions.Add(Permissions.Endpoints.Authorization);
        descriptor.Permissions.Add(Permissions.Endpoints.Token);
        descriptor.Permissions.Add(Permissions.GrantTypes.AuthorizationCode);
        if (document.GrantTypes.Contains(GrantTypes.RefreshToken))
        {
            descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);
        }

        descriptor.Permissions.Add(Permissions.ResponseTypes.Code);
        descriptor.Permissions.UnionWith(hostPermissions);

        descriptor.Requirements.Clear();
        descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);

        descriptor.Properties[ClientIdMetadataDocumentRegistration.ExpiresAtProperty] =
            ClientIdMetadataDocumentRegistration.CreateExpiresAt(expiresAt);
    }

    /// <summary>
    /// A registration still within its lifetime takes the host's current scopes and resources, so a resource added to
    /// the server (or a scope removed) applies at the client's next authorization rather than at its next fetch.
    /// </summary>
    private async Task UpdateHostPermissionsIfChangedAsync(
        object application,
        IReadOnlySet<string> hostPermissions,
        CancellationToken cancellationToken)
    {
        var permissions = await ApplicationManager.GetPermissionsAsync(application, cancellationToken);
        if (ClientIdMetadataDocumentRegistration.HasExactlyHostPermissions(permissions, hostPermissions))
        {
            return;
        }

        var descriptor = new OpenIddictApplicationDescriptor();
        await ApplicationManager.PopulateAsync(descriptor, application, cancellationToken);
        descriptor.Permissions.RemoveWhere(ClientIdMetadataDocumentRegistration.IsHostPermission);
        descriptor.Permissions.UnionWith(hostPermissions);
        await ApplicationManager.UpdateAsync(application, descriptor, cancellationToken);
    }
}
