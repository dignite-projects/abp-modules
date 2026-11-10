```json
//[doc-seo]
{
    "Description": "Keep an ABP application's BLOB container in Dignite Drive with UseDrive: the Drive in the same process or remote, how files are named and de-duplicated, and public addresses with image sizing."
}
```

# Using the Drive From an Application

> Drive has not been released yet; see the [overview](index.md).

An application keeps a BLOB container's bytes in the Drive with one provider package, `Dignite.Abp.BlobStoring.Drive`. Its code does not change: it still saves and reads through ABP's `IBlobContainer`, with its own pipeline contributors and its own authorization.

## Installation

Reference `Dignite.Abp.BlobStoring.Drive` in the module that configures the container, and depend on `AbpBlobStoringDriveModule`:

````csharp
[DependsOn(typeof(AbpBlobStoringDriveModule))]
public class MyModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.Configure<ProfilePictureContainer>(container =>
            {
                // The application's own rules run first, in the application.
                container.AddMaxSizeContributor(c => c.MaxSizeInBytes = 5 * 1024 * 1024);
                container.AddAllowedContentTypesContributor(c => c.AllowedContentTypes = new[] { "image/jpeg", "image/png" });

                // Then the bytes go to the Drive container "media".
                container.UseDrive(drive => drive.DriveContainer = "media");
            });
        });
    }
}
````

`DriveContainer` names a system container declared on the Drive ([Setting Up the Drive](drive-host.md#drive-containers)). It need not have the application container's name. Several application containers may use the same Drive container - and then share its names: a name saved through one is the same file through the other.

Whether the Drive is in this process or a remote service is the **host's** choice, not the module's:

* **In the same process**: the host installs the Drive itself ([Setting Up the Drive](drive-host.md)). The provider calls it directly, as a trusted caller.
* **Remote**: the host installs `Dignite.Drive.HttpApi.Client` and depends on its module, `DriveHttpApiClientModule`. The provider calls the Drive's HTTP API through ABP's C# client proxies, as an OAuth client.

A host that has installed neither fails at the first use of a Drive container, with a message saying which to install.

## The Remote Drive

The proxies use the `Drive` [remote service](https://abp.io/docs/latest/framework/api-development/static-csharp-clients). The Drive accepts **client credentials only**: a token that carries a user is refused, whatever its client - an application calls with its own identity, and browsers use [tickets](tickets.md). Configure the address, and a client-credentials identity client with ABP's `Volo.Abp.Http.Client.IdentityModel` (depend on `AbpHttpClientIdentityModelModule`):

````json
{
  "RemoteServices": {
    "Drive": {
      "BaseUrl": "https://drive.example.com/",
      "IdentityClient": "DriveClient"
    }
  },
  "IdentityClients": {
    "DriveClient": {
      "GrantType": "client_credentials",
      "ClientId": "site-app",
      "ClientSecret": "...",
      "Authority": "https://auth.example.com",
      "Scope": "Drive"
    }
  }
}
````

The client - `site-app` here - must be in the Drive container's `AllowedClientIds`. The tenant travels with each call, as ABP's proxies send it.

## What the Drive Does With a File

* **Names.** The file is addressed by the name the application saves it under, which may be a path (`2026/11/photo.png`). Saving a taken name fails with ABP's `BlobAlreadyExistsException` unless `overrideExisting` is set; overwriting replaces the file.
* **Rules.** The application container's contributors run in the application; the Drive container's run in the Drive, on what the application sent. An image the Drive container resizes is stored resized.
* **Type.** The Drive detects the type from the bytes, and refuses a name whose extension the content contradicts (`Dignite.Abp.BlobStoring:ContentTypeMismatch`).
* **De-duplication.** Bytes already stored in the same Drive container, for the same tenant, are not stored again: the new file shares them. Deleting a file deletes its bytes only when no other file shares them.
* **Tenants.** A multi-tenant application container stores in the current tenant, one that is not multi-tenant stores for the host - as with any provider - and the tenant's [storage quota](drive-host.md#tenant-storage) applies.

## Public Addresses

A file in a Drive container that is read publicly (`PublicRead`) can be served to anyone, without signing in. `IDrivePublicUrlBuilder` gives its address:

````csharp
var url = _publicUrlBuilder.Build(
    "profile-pictures",          // the application's container
    "2026/11/photo.png",
    width: 256);                 // optional: width, height
// /api/drive/public/media/2026/11/photo.png?__tenant=…&Width=256
````

* The address names the Drive container and the file. When the file belongs to a tenant it carries `__tenant=`: the request is anonymous, so nothing else says whose file it is. ABP's query-string tenant resolver, on by default, reads it.
* `Width` and `Height` resize a JPEG, PNG or WebP: with both, the picture is cropped to fill the box; with one, the other follows its proportions. A picture is never enlarged, and one larger than 4096 pixels on a side is served as stored. Other files are always served as stored.
* Addresses are relative by default, which is right when the Drive is in the application's process. When the Drive runs on its own, set where it is reached from:

````csharp
Configure<AbpBlobStoringDriveOptions>(options =>
{
    options.PublicBaseUrl = "https://drive.example.com";
});
````

The response is cacheable by anyone for a day, with an `ETag` that changes when the file is overwritten (a matching `If-None-Match` is answered `304`). It never sets a cookie, is marked `nosniff`, and serves a type outside the Drive's list of known types as a download. `HEAD` is answered without reading the file. A file whose container is not read publicly is not found.

## See Also

* [Tickets](tickets.md)
* [Hosting Modes](hosting.md)
