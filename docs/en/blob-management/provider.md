```json
//[doc-seo]
{
    "Description": "Keep an ABP application's BLOB container in Dignite Blob Management with UseBlobManagement: the module in the same process or remote, how files are named and de-duplicated, and public addresses with image sizing."
}
```

# Using It From an Application

> Blob Management has not been released yet; see the [overview](index.md).

An application keeps a BLOB container's bytes in a managed container with one provider package, `Dignite.Abp.BlobStoring.BlobManagement`. Its code does not change: it still saves and reads through ABP's `IBlobContainer`, with its own pipeline contributors and its own authorization.

## Installation

Reference `Dignite.Abp.BlobStoring.BlobManagement` in the module that configures the container, and depend on `AbpBlobStoringBlobManagementModule`:

````csharp
[DependsOn(typeof(AbpBlobStoringBlobManagementModule))]
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

                // Then the bytes go to the managed container "media".
                container.UseBlobManagement(blob => blob.BlobManagementContainer = "media");
            });
        });
    }
}
````

`BlobManagementContainer` names a container declared on the Blob Management host ([Managed Containers](setting-up.md#managed-containers)). It need not have the application container's name. Several application containers may use the same managed container - and then share its names: a name saved through one is the same file through the other.

Whether Blob Management is in this process or a remote service is the **host's** choice, not the module's:

* **In the same process**: the host installs Blob Management itself ([Setting Up](setting-up.md)). The provider calls it directly, as a trusted caller.
* **Remote**: the host installs `Dignite.Abp.BlobManagement.HttpApi.Client` and depends on its module, `BlobManagementHttpApiClientModule`. The provider calls the BLOB API through ABP's C# client proxies, as an OAuth client.

A host that has installed neither fails at the first use of such a container, with a message saying which to install.

## A Remote Blob Management

The proxies use the `BlobManagement` [remote service](https://abp.io/docs/latest/framework/api-development/static-csharp-clients). The BLOB API accepts **client credentials only**: a token that carries a user is refused, whatever its client - an application calls with its own identity, and browsers use [tickets](tickets.md). Configure the address, and a client-credentials identity client with ABP's `Volo.Abp.Http.Client.IdentityModel` (depend on `AbpHttpClientIdentityModelModule`):

````json
{
  "RemoteServices": {
    "BlobManagement": {
      "BaseUrl": "https://blobs.example.com/",
      "IdentityClient": "BlobManagementClient"
    }
  },
  "IdentityClients": {
    "BlobManagementClient": {
      "GrantType": "client_credentials",
      "ClientId": "site-app",
      "ClientSecret": "...",
      "Authority": "https://auth.example.com",
      "Scope": "BlobManagement"
    }
  }
}
````

The client - `site-app` here - must be in the managed container's `AllowedClientIds`; the scope is the one under which your authorization server knows the Blob Management service. The tenant travels with each call, as ABP's proxies send it.

## What Blob Management Does With a File

* **Names.** The file is addressed by the name the application saves it under, which may be a path (`2026/11/photo.png`). Saving a taken name fails with ABP's `BlobAlreadyExistsException` unless `overrideExisting` is set; overwriting replaces the file. Such a file sits at the top of its container, outside any directory, and is never renamed or moved.
* **Rules.** The application container's contributors run in the application; the managed container's run in Blob Management, on what the application sent. An image the managed container resizes is stored resized.
* **Type.** The type is detected from the bytes, and a name whose extension the content contradicts is refused (`Dignite.Abp.BlobStoring:ContentTypeMismatch`).
* **De-duplication.** Bytes already stored in the same managed container, for the same tenant, are not stored again: the new file shares them. Deleting a file deletes its bytes only when no other file shares them.
* **Tenants.** A multi-tenant application container stores in the current tenant, one that is not multi-tenant stores for the host - as with any provider - and the tenant's [storage quota](setting-up.md#tenant-storage) applies.

The files an application saves are listed in the back office beside the files people upload, and a user with the permission to delete files there may delete them.

## Public Addresses

A file in a managed container that is read publicly (`PublicRead`) can be served to anyone, without signing in. `IBlobManagementPublicUrlBuilder` gives the address of a file the application saved:

````csharp
var url = _publicUrlBuilder.Build(
    "profile-pictures",          // the application's container
    "2026/11/photo.png",
    width: 256);                 // optional: width, height
// /api/blob-management/public/media/2026/11/photo.png?__tenant=…&Width=256
````

* The address names the managed container and the file. When the file belongs to a tenant it carries `__tenant=`: the request is anonymous, so nothing else says whose file it is. ABP's query-string tenant resolver, on by default, reads it.
* `Width` and `Height` resize a JPEG, PNG or WebP: with both, the picture is cropped to fill the box; with one, the other follows its proportions. A picture is never enlarged, and one larger than 4096 pixels on a side is served as stored. Other files are always served as stored.
* **A size must be one the host allows.** The endpoint is anonymous and renders a size on every request, so an open range would let a visitor make the host render a different picture for each request. By default a `Width` or `Height` must be one of `64`, `128`, `256`, `384`, `512`, `768`, `1024`, `1536`, `2048` or `4096`; any other value is answered `400 Bad Request` (for `GET` and `HEAD` alike), before the file is read. A size is not rounded to the nearest allowed one: with both sides given the picture is cropped to the box, and rounding the sides independently would change its shape. `IBlobManagementPublicUrlBuilder` does not check the size, because Blob Management may be a remote one whose list the application cannot know. A request without a size is unaffected.
* Addresses are relative by default, which is right when Blob Management is in the application's process. When it runs on its own, set where it is reached from:

````csharp
Configure<AbpBlobStoringBlobManagementOptions>(options =>
{
    options.PublicBaseUrl = "https://blobs.example.com";
});
````

A file a person uploaded has a public address too, given as the `url` of the file ([Public Reads](attaching-files.md#public-reads)): it names the file's physical BLOB rather than its name, so renaming or moving the file never breaks a published link.

The response is cacheable by anyone for a day, with an `ETag` that changes when the file is overwritten (a matching `If-None-Match` is answered `304`). It never sets a cookie, is marked `nosniff`, and serves a type outside the module's list of known types as a download. `HEAD` is answered without reading the file. A file whose container is not read publicly is not found.

The host sets the allowed sizes with `BlobManagementPublicReadOptions` (in `Dignite.Abp.BlobManagement.Application`); the list replaces the default, so clear it first to restrict it and add to it to extend it. A value above 4096 is never served, whatever the list says:

````csharp
Configure<BlobManagementPublicReadOptions>(options =>
{
    options.AllowedSizes.Clear();
    options.AllowedSizes.AddRange(new[] { 96, 192, 480, 960 });
});
````

There is no server-side cache of renditions: a size is rendered again on each request that is not answered by an HTTP cache. A CDN, reverse proxy or rate limit in front of the host is still the host's job; the list of sizes only bounds how many different renditions an address can ask for.

## See Also

* [Tickets](tickets.md)
* [Hosting Modes](hosting.md)
