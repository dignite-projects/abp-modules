using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using OpenIddict.Abstractions;

namespace Dignite.Abp.OpenIddict.ClientIdMetadataDocuments;

/// <summary>
/// What marks an application as a CIMD registration, and the permissions the host - not the document - decides.
/// </summary>
internal static class ClientIdMetadataDocumentRegistration
{
    /// <summary>
    /// When the registration expires (UTC, ISO 8601), in the application's Properties. Only CIMD registrations carry
    /// it: an application without it was registered some other way and is never touched.
    /// </summary>
    public const string ExpiresAtProperty = "cimd_expires_at";

    public static JsonElement CreateExpiresAt(DateTimeOffset expiresAt) =>
        JsonSerializer.SerializeToElement(expiresAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));

    public static bool TryGetExpiresAt(ImmutableDictionary<string, JsonElement> properties, out DateTimeOffset expiresAt)
    {
        expiresAt = default;
        return properties.TryGetValue(ExpiresAtProperty, out var value) && TryParse(value, out expiresAt);
    }

    /// <summary>Reads the expiry from an application entity's raw Properties column.</summary>
    public static bool TryGetExpiresAt(string? properties, out DateTimeOffset expiresAt)
    {
        expiresAt = default;
        if (string.IsNullOrEmpty(properties))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(properties);
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty(ExpiresAtProperty, out var value)
                   && TryParse(value, out expiresAt);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// The scope and resource permissions a CIMD client gets: one <c>scp:</c> per configured scope, and one
    /// <c>rsrc:</c> per resource the host registered with <c>RegisterResources</c>, in both trailing-slash forms.
    /// OpenIddict compares the request's <c>resource</c> parameter with the permission as a raw string, and which form
    /// a client sends depends on how it echoes the identifier it discovered; a missing form fails as ID2192.
    /// </summary>
    public static IReadOnlySet<string> GetHostPermissions(IEnumerable<string> scopes, IEnumerable<Uri> resources)
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scope in scopes)
        {
            permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);
        }

        foreach (var resource in resources)
        {
            var value = resource.AbsoluteUri;
            permissions.Add(OpenIddictConstants.Permissions.Prefixes.Resource + value);

            if (string.IsNullOrEmpty(resource.Query) && string.IsNullOrEmpty(resource.Fragment))
            {
                permissions.Add(OpenIddictConstants.Permissions.Prefixes.Resource
                                + (value.EndsWith('/') ? value[..^1] : value + "/"));
            }
        }

        return permissions;
    }

    /// <summary>The permissions <see cref="GetHostPermissions"/> decides; the rest come from the document.</summary>
    public static bool IsHostPermission(string permission) =>
        permission.StartsWith(OpenIddictConstants.Permissions.Prefixes.Scope, StringComparison.Ordinal)
        || permission.StartsWith(OpenIddictConstants.Permissions.Prefixes.Resource, StringComparison.Ordinal);

    public static bool HasExactlyHostPermissions(IEnumerable<string> permissions, IReadOnlySet<string> hostPermissions) =>
        hostPermissions.SetEquals(permissions.Where(IsHostPermission));

    private static bool TryParse(JsonElement value, out DateTimeOffset expiresAt)
    {
        expiresAt = default;
        return value.ValueKind == JsonValueKind.String
               && DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out expiresAt);
    }
}
