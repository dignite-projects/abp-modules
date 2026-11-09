using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace Dignite.Abp.OpenIddict.ClientIdMetadataDocuments;

/// <summary>
/// An authorization request with an https client_id gets its document fetched and its client registered before
/// OpenIddict's own client validation looks the client_id up. Only the authorization endpoint: by the code exchange
/// and refresh, the client is registered.
/// </summary>
internal sealed class ProvisionClientIdMetadataDocumentClient : IOpenIddictServerHandler<ValidateAuthorizationRequestContext>
{
    private readonly ClientIdMetadataDocumentClientProvisioner _provisioner;
    private readonly AbpClientIdMetadataDocumentOptions _options;

    public ProvisionClientIdMetadataDocumentClient(
        ClientIdMetadataDocumentClientProvisioner provisioner,
        IOptions<AbpClientIdMetadataDocumentOptions> options)
    {
        _provisioner = provisioner;
        _options = options.Value;
    }

    /// <summary>
    /// After OpenIddict requires the client_id parameter (ValidateClientIdParameter) and before ValidateAuthentication,
    /// which looks the client up in the application store and refuses an unknown one as invalid_client.
    /// </summary>
    public static OpenIddictServerHandlerDescriptor Descriptor { get; }
        = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateAuthorizationRequestContext>()
            .UseScopedHandler<ProvisionClientIdMetadataDocumentClient>()
            .SetOrder(OpenIddictServerHandlers.Authentication.ValidateClientIdParameter.Descriptor.Order + 500)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(ValidateAuthorizationRequestContext context)
    {
        if (!_options.IsEnabled || !ClientIdMetadataDocumentRules.IsClientIdUrl(context.ClientId))
        {
            return;
        }

        var error = await _provisioner.EnsureAsync(context.ClientId!, context.CancellationToken);
        if (error != null)
        {
            context.Reject(error: Errors.InvalidClient, description: error);
        }
    }
}

/// <summary>
/// Advertises CIMD support in the authorization server metadata. A client (ChatGPT, Claude) uses CIMD rather than
/// dynamic registration only when it sees <c>client_id_metadata_document_supported: true</c> and <c>none</c> among the
/// token endpoint's authentication methods.
/// </summary>
internal sealed class AttachClientIdMetadataDocumentSupport : IOpenIddictServerHandler<HandleConfigurationRequestContext>
{
    public const string MetadataName = "client_id_metadata_document_supported";

    private readonly AbpClientIdMetadataDocumentOptions _options;

    public AttachClientIdMetadataDocumentSupport(IOptions<AbpClientIdMetadataDocumentOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// After AttachClientAuthenticationMethods, which unions the server's client authentication methods into the
    /// token endpoint's list - run before it, and private_key_jwt would be put back after being removed.
    /// </summary>
    public static OpenIddictServerHandlerDescriptor Descriptor { get; }
        = OpenIddictServerHandlerDescriptor.CreateBuilder<HandleConfigurationRequestContext>()
            .UseSingletonHandler<AttachClientIdMetadataDocumentSupport>()
            .SetOrder(OpenIddictServerHandlers.Discovery.AttachClientAuthenticationMethods.Descriptor.Order + 500)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public ValueTask HandleAsync(HandleConfigurationRequestContext context)
    {
        if (!_options.IsEnabled)
        {
            return default;
        }

        context.Metadata[MetadataName] = true;
        context.TokenEndpointAuthenticationMethods.Add(ClientIdMetadataDocumentRules.TokenEndpointAuthMethodNone);

        if (_options.RemovePrivateKeyJwtFromDiscovery)
        {
            // Claude, seeing private_key_jwt, switches to its confidential-client document (…/confidential_client)
            // and authenticates with a signed assertion; a CIMD registration here is a public client, so sign-in fails.
            context.TokenEndpointAuthenticationMethods.Remove(ClientAuthenticationMethods.PrivateKeyJwt);
        }

        return default;
    }
}
