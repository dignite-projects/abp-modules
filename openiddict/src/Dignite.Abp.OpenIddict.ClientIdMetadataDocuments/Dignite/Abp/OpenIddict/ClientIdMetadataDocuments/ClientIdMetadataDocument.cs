using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;

namespace Dignite.Abp.OpenIddict.ClientIdMetadataDocuments;

/// <summary>A fetched and validated client metadata document, reduced to the fields the server uses.</summary>
internal sealed class ClientIdMetadataDocument
{
    public required string ClientId { get; init; }

    public required string ClientName { get; init; }

    public required IReadOnlyList<Uri> RedirectUris { get; init; }

    public required IReadOnlyList<string> GrantTypes { get; init; }

    /// <summary>
    /// A loopback redirect URI (localhost, 127.0.0.1, [::1]) makes the client a native application, the only kind
    /// whose redirect URI port OpenIddict ignores (RFC 8252).
    /// </summary>
    public bool HasLoopbackRedirect => RedirectUris.Any(ClientIdMetadataDocumentRules.IsLoopback);
}

/// <summary>A document or client_id that breaks the rules. The message is returned as the error_description.</summary>
internal sealed class ClientIdMetadataDocumentException : Exception
{
    public ClientIdMetadataDocumentException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// The rules for a client_id URL and its document, from the Client ID Metadata Documents section of the MCP
/// authorization specification and draft-ietf-oauth-client-id-metadata-document. Independent of OpenIddict.
/// </summary>
internal static class ClientIdMetadataDocumentRules
{
    public const string TokenEndpointAuthMethodNone = "none";

    private static readonly HashSet<string> SupportedGrantTypes = new(StringComparer.Ordinal)
    {
        "authorization_code",
        "refresh_token"
    };

    /// <summary>
    /// An https client_id is a metadata document URL; any other client_id is looked up in the application store as
    /// usual, so pre-registered clients keep working.
    /// </summary>
    public static bool IsClientIdUrl(string? clientId) =>
        clientId != null && clientId.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The draft's requirements for the URL: https, a path, no "." or ".." segment, no fragment, no user info.
    /// A query string is allowed.
    /// </summary>
    public static Uri ParseClientIdUrl(string clientId)
    {
        if (!Uri.TryCreate(clientId, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ClientIdMetadataDocumentException("client_id must be an absolute https URL.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ClientIdMetadataDocumentException("client_id must not contain a username or password.");
        }

        if (clientId.Contains('#'))
        {
            throw new ClientIdMetadataDocumentException("client_id must not contain a fragment.");
        }

        // Uri normalizes "." and ".." away, so the raw string's path is checked.
        var authorityEnd = clientId.IndexOf('/', "https://".Length);
        if (authorityEnd < 0 || uri.AbsolutePath == "/")
        {
            throw new ClientIdMetadataDocumentException("client_id must contain a path component.");
        }

        var path = clientId[authorityEnd..].Split('?', 2)[0];
        if (path.Split('/').Any(segment => segment is "." or ".."
                                           || segment.Equals("%2e", StringComparison.OrdinalIgnoreCase)
                                           || segment.Equals("%2e%2e", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ClientIdMetadataDocumentException("client_id must not contain dot path segments.");
        }

        return uri;
    }

    public static ClientIdMetadataDocument Parse(string clientId, ReadOnlySpan<byte> json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json.ToArray());
        }
        catch (JsonException)
        {
            throw new ClientIdMetadataDocumentException("The client metadata document is not valid JSON.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ClientIdMetadataDocumentException("The client metadata document must be a JSON object.");
            }

            var documentClientId = GetString(root, "client_id");
            if (!string.Equals(documentClientId, clientId, StringComparison.Ordinal))
            {
                throw new ClientIdMetadataDocumentException("The client_id in the metadata document does not match the document URL.");
            }

            var clientName = GetString(root, "client_name");
            if (string.IsNullOrWhiteSpace(clientName))
            {
                throw new ClientIdMetadataDocumentException("The client metadata document must include client_name.");
            }

            var redirectUris = GetStringArray(root, "redirect_uris")
                               ?? throw new ClientIdMetadataDocumentException("The client metadata document must include redirect_uris.");
            if (redirectUris.Count == 0)
            {
                throw new ClientIdMetadataDocumentException("redirect_uris must not be empty.");
            }

            var parsedRedirectUris = redirectUris.Select(ParseRedirectUri).ToList();

            // Only public clients (none + PKCE) are registered. A client may declare several methods: ChatGPT puts
            // private_key_jwt in the singular token_endpoint_auth_method as its preference and lists none in
            // token_endpoint_auth_methods_supported, then picks from what both sides support - and this server
            // advertises none. So the document is accepted as long as none is among the methods it declares.
            var authMethods = GetStringArray(root, "token_endpoint_auth_methods_supported")
                              ?? new List<string> { GetString(root, "token_endpoint_auth_method") ?? TokenEndpointAuthMethodNone };
            if (!authMethods.Contains(TokenEndpointAuthMethodNone))
            {
                throw new ClientIdMetadataDocumentException(
                    $"token endpoint auth method(s) '{string.Join(", ", authMethods)}' are not supported; only 'none' (public client with PKCE) is accepted.");
            }

            // Likewise for grant types: the client lists what it can use, the server grants the intersection
            // (authorization_code, plus refresh_token if listed) and ignores the rest rather than refusing the document,
            // as RFC 7591 lets a server replace values it does not support. Since 2026-11-07 Claude's document also lists
            // urn:ietf:params:oauth:grant-type:jwt-bearer; refusing on it locked Claude out.
            var declaredGrantTypes = GetStringArray(root, "grant_types") ?? new List<string> { "authorization_code" };
            if (!declaredGrantTypes.Contains("authorization_code"))
            {
                throw new ClientIdMetadataDocumentException("grant_types must include authorization_code.");
            }

            var responseTypes = GetStringArray(root, "response_types") ?? new List<string> { "code" };
            if (responseTypes.Any(r => r != "code"))
            {
                throw new ClientIdMetadataDocumentException("response_types may only contain 'code'.");
            }

            return new ClientIdMetadataDocument
            {
                ClientId = clientId,
                ClientName = clientName.Trim(),
                RedirectUris = parsedRedirectUris,
                GrantTypes = declaredGrantTypes.Where(SupportedGrantTypes.Contains).Distinct().ToList()
            };
        }
    }

    /// <summary>The MCP authorization specification allows only https and loopback http redirect URIs.</summary>
    public static Uri ParseRedirectUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || value.Contains('#'))
        {
            throw new ClientIdMetadataDocumentException($"redirect_uri '{value}' is not a valid absolute URL without a fragment.");
        }

        if (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && IsLoopback(uri)))
        {
            return uri;
        }

        throw new ClientIdMetadataDocumentException($"redirect_uri '{value}' must use https or a loopback http address.");
    }

    public static bool IsLoopback(Uri uri) =>
        uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(uri.Host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address));

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static List<string>? GetStringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Array || value.EnumerateArray().Any(e => e.ValueKind != JsonValueKind.String))
        {
            throw new ClientIdMetadataDocumentException($"{name} must be an array of strings.");
        }

        return value.EnumerateArray().Select(e => e.GetString()!).ToList();
    }
}
