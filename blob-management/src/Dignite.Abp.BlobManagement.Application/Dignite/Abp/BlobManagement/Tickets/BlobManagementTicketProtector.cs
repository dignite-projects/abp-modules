using System;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.BlobManagement.Tickets;

/// <summary>
/// Signs and reads Blob Management's tickets - the upload tickets and the read links a browser holds - with ASP.NET
/// Core Data Protection's time-limited protector, one purpose per kind of ticket. A ticket is the JSON of what it
/// allows, encrypted and authenticated with the host's key ring, with its expiration inside; it is base64url, so it
/// travels in a URL as it is.
/// <para>
/// The keys never leave Blob Management: a business system asks for a ticket and hands it on, and only a Blob
/// Management instance that shares the key ring can read it. Several instances therefore need one persisted key ring
/// and one application name (<c>AddDataProtection().PersistKeysTo…().SetApplicationName(…)</c>), kept to Blob
/// Management's instances.
/// </para>
/// </summary>
public class BlobManagementTicketProtector : ITransientDependency
{
    protected IDataProtectionProvider DataProtectionProvider { get; }

    public BlobManagementTicketProtector(IDataProtectionProvider dataProtectionProvider)
    {
        DataProtectionProvider = dataProtectionProvider;
    }

    /// <summary>The ticket for <paramref name="payload"/>, accepted until <paramref name="expiration"/>.</summary>
    public virtual string Protect<T>(string purpose, T payload, DateTimeOffset expiration)
    {
        return CreateProtector(purpose).Protect(JsonSerializer.Serialize(payload), expiration);
    }

    /// <summary>
    /// What <paramref name="ticket"/> carries, when it was issued for <paramref name="purpose"/> by this key ring and has
    /// not expired; <c>null</c> otherwise - an expired, altered or foreign ticket, or no ticket at all.
    /// </summary>
    public virtual T? Unprotect<T>(string purpose, string? ticket, out DateTimeOffset expiration)
        where T : class
    {
        expiration = default;
        if (string.IsNullOrWhiteSpace(ticket))
        {
            return null;
        }

        try
        {
            var json = CreateProtector(purpose).Unprotect(ticket, out expiration);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException)
        {
            expiration = default;
            return null;
        }
    }

    protected virtual ITimeLimitedDataProtector CreateProtector(string purpose)
    {
        return DataProtectionProvider.CreateProtector(purpose).ToTimeLimitedDataProtector();
    }
}
