```json
//[doc-seo]
{
    "Description": "Dignite Drive tickets: an ABP application authorizes its user, asks the Drive for a short-lived signed ticket, and the browser uploads or downloads the file directly - what is checked, and what an upload with a ticket skips."
}
```

# Tickets

> Drive has not been released yet; see the [overview](index.md).

A **ticket** lets a browser send one file to the Drive, or fetch one, directly - without the bytes passing through the application. The application decides who may, by its own rules; the Drive checks only the ticket.

````text
Browser ──► Application: "I want to upload avatar.png"
            the application authorizes its user, by its own rules
            Application ──► Drive: IDriveTicketClient.CreateUploadTicketAsync(...)   (as an allowed client)
            ◄── ticket + address
Browser ──► Drive: POST {address}  (the file)
            the Drive checks the ticket, checks its issuer again, stores the file
````

## Issuing

`IDriveTicketClient`, in the provider package, asks for a ticket for one file of an application container configured with [`UseDrive`](provider.md):

````csharp
public async Task<string> GetAvatarUploadUrlAsync()
{
    // The application's own authorization first: the Drive does not know its users.
    await CheckPolicyAsync(MyPermissions.Profile.Edit);

    var ticket = await _ticketClient.CreateUploadTicketAsync(
        "profile-pictures",                      // the application's container
        $"avatars/{CurrentUser.Id}.png",
        maxSizeInBytes: 2 * 1024 * 1024,         // optional: below the Drive container's own limit
        overrideExisting: true,
        lifetime: TimeSpan.FromMinutes(5));      // optional: 10 minutes by default, at most an hour

    return ticket.Url;
}
````

`CreateDownloadTicketAsync` asks for a ticket to read one file. `DriveTicketDto.Url` is the address to give the browser - `/api/drive/tickets/upload?ticket=…` or `/api/drive/tickets/download?ticket=…`, after `AbpBlobStoringDriveOptions.PublicBaseUrl`. The ticket names its tenant itself.

The application asks as it stores: in the same process as a trusted caller, remotely as an OAuth client the Drive container allows ([The Remote Drive](provider.md#the-remote-drive)). Tickets are issued for system containers.

## Redeeming

The browser uploads with a form post, the file in the field `content`:

````js
const form = new FormData();
form.append('content', file);
const response = await fetch(uploadUrl, { method: 'POST', body: form });
// 200: { name, mimeType, size, hash }
````

and downloads by following the download address. The redemption addresses are anonymous - the ticket is the authorization - and exempt from antiforgery validation.

When a ticket is redeemed, the Drive checks that it signed it and that it has not expired, that it is redeemed as the kind it is, and that **its issuer is still allowed** in the container: an application removed from the container's `AllowedClientIds` after issuing a ticket cannot have it used. Then the work runs in the ticket's tenant. Any of these failing answers `Drive:InvalidTicket` (HTTP 403), whichever it was; the reason is logged.

* An **upload** ticket is good for one successful upload. An upload that failed - too large, a type the container refuses - does not use it, and can be tried again until it expires. The ticket's own size limit applies, then the Drive container's.
* A **download** ticket can be used until it expires, because a browser may fetch a file more than once. It is served as a download, never cached.

## What an Upload With a Ticket Skips

The bytes of a ticketed upload never reach the application, so **the application container's own pipeline contributors do not run on them** - only the Drive container's. If the application resizes, converts or checks its uploads with contributors of its own, an upload that needs them cannot use a ticket: let it go through the application, or give the Drive container rules that do the same work.

## Running Several Drive Instances

A ticket is signed with ASP.NET Core [Data Protection](https://learn.microsoft.com/aspnet/core/security/data-protection/introduction), and an upload's use is recorded in the [distributed cache](https://abp.io/docs/latest/framework/fundamentals/caching) under a [distributed lock](https://abp.io/docs/latest/framework/infrastructure/distributed-locking). With more than one instance of the Drive, all of them need the same Data Protection key ring, and a shared cache and lock provider (Redis, for example) - see [Hosting Modes](hosting.md#the-drive-as-its-own-service).

Users who [attach files](attaching-files.md) have upload links of their own, issued and redeemed as a user: see [Upload Links for Users](attaching-files.md#upload-links-for-users).

## See Also

* [Using the Drive From an Application](provider.md)
