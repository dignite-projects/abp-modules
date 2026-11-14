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

The proxies use the `BlobManagement` [remote service](https://abp.io/docs/latest/framework/api-development/static-csharp-clients). The BLOB API accepts **client credentials only**: a token that carries a user is refused, whatever its client - an application calls with its own identity, for its user, after applying its own rules. Configure the address, and a client-credentials identity client with ABP's `Volo.Abp.Http.Client.IdentityModel` (depend on `AbpHttpClientIdentityModelModule`):

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

The client - `site-app` here - must be granted the managed container's files ([Granting an Application](permissions.md#granting-an-application)); the scope is the one under which your authorization server knows the Blob Management service. The tenant travels with each call, as ABP's proxies send it. The same configuration serves the [file service](files.md), which calls the same remote service.

A browser does not reach a remote Blob Management for a file that is not public: the application reads the bytes through the provider and hands them on, and receives an upload and stores it.

## What Blob Management Does With a File

* **Names.** The file is addressed by the name the application saves it under, which may be a path (`2026/11/photo.png`). Saving a taken name fails with ABP's `BlobAlreadyExistsException` unless `overrideExisting` is set; overwriting replaces the file. Such a file sits at the top of its container, outside any directory, and is never renamed or moved.
* **Rules.** The application container's contributors run in the application; the managed container's run in Blob Management, on what the application sent. An image the managed container resizes is stored resized.
* **Type.** The type is detected from the bytes, and a name whose extension the content contradicts is refused (`Dignite.Abp.BlobStoring:ContentTypeMismatch`).
* **De-duplication.** Bytes already stored in the same managed container, for the same tenant, are not stored again: the new file shares them. Deleting a file deletes its bytes only when no other file shares them.
* **Tenants.** A multi-tenant application container stores in the current tenant, one that is not multi-tenant stores for the host - as with any provider - and the tenant's [storage allocation](setting-up.md#tenant-storage) applies.
* **The user it is counted for.** Each file records the user it is counted for in the [usage figures](usage.md), and nothing else comes with it. In the same process it is the application's current user, as ABP's auditing records it. Remotely the client's token carries no user, so `Dignite.Abp.BlobManagement.HttpApi.Client` sends the application's current user with the save; a file stored over HTTP also records the OAuth client that stored it.

The files an application saves are listed, read-only, on the [management page](angular-ui.md#the-management-page) beside the files it keeps by id.

## Calling the BLOB Service Directly

The provider calls `IBlobManagementBlobService`, which an application may call itself - to name the user a BLOB is counted for, for instance, when it stores for someone other than its current user:

````csharp
await _blobService.SaveAsync(
    "media",                    // the managed container
    "2026/11/photo.png",
    new RemoteStreamContent(stream, "photo.png"),
    overrideExisting: false,
    creatorId: userId);         // null: the application's current user
````

It also has `GetOrNullAsync`, `ExistsAsync` and `DeleteAsync`, by container and name. Over HTTP it is `api/blob-management/blobs/{containerName}`, with the name in `blobName`.

## Public Addresses

A file the application marks public can be served to anyone, without signing in. For the BLOBs it saves by name, the application declares it on its own container, and the provider sends it with every save:

````csharp
options.Containers.Configure<ProfilePictureContainer>(container =>
{
    container.UseBlobManagement(provider =>
    {
        provider.BlobManagementContainer = "media";
        provider.IsPublic = true;
    });
});
````

The mark is the file's, written when it is saved: turning `IsPublic` on or off changes what is saved from then on, and a BLOB saved before keeps what it was saved with until it is saved again. Whether anyone may read a file is the business system's call, not Blob Management's: a managed container has no setting for it. (A file kept by id is marked by its own call, [Public Files](files.md#public-files).)

`IBlobManagementPublicUrlBuilder` gives the address of a file the application saved:

````csharp
var url = _publicUrlBuilder.Build(
    "profile-pictures",          // the application's container
    "2026/11/photo.png",
    width: 256);                 // optional: width, height
// /blobs/media/2026/11/photo.png?__tenant=…&Width=256
````

* Public reads are at `/blobs/{container}/{name}`: an address a page embeds and a CDN caches, not an API, so it is not under `/api`. A host behind a gateway routes `/blobs/*` to Blob Management as well ([Hosting Modes](hosting.md#blob-management-as-its-own-service)).

* The address names the managed container and the file. When the file belongs to a tenant it carries `__tenant=`: the request is anonymous, so nothing else says whose file it is. ABP's query-string tenant resolver, on by default, reads it.
* `Width` and `Height` resize a JPEG, PNG or WebP: with both, the picture is cropped to fill the box; with one, the other follows its proportions. A picture is never enlarged, and one larger than 4096 pixels on a side is served as stored. Other files are always served as stored.
* **A size must be one the container allows.** The endpoint is anonymous and renders a size on every request, so an open range would let a visitor make the host render a different picture for each request. By default a `Width` or `Height` must be one of `64`, `128`, `256`, `384`, `512`, `768`, `1024`, `1536`, `2048` or `4096`; any other value is answered `400 Bad Request` (for `GET` and `HEAD` alike), before the file is read. Width and height each take a value from the list, in any combination. A size is not rounded to the nearest allowed one: with both sides given the picture is cropped to the box, and rounding the sides independently would change its shape. `IBlobManagementPublicUrlBuilder` does not check the size, because Blob Management may be a remote one whose lists the application cannot know; a container's list is its `publicSizes` in the container API. A request without a size is unaffected.
* Addresses are relative by default, which is right when Blob Management is in the application's process. When it runs on its own, set where it is reached from:

````csharp
Configure<AbpBlobStoringBlobManagementOptions>(options =>
{
    options.PublicBaseUrl = "https://blobs.example.com";
});
````

A file kept by id has a public address too, given as the `url` of the file ([Public Reads](files.md#public-reads)): it names the file's physical BLOB rather than its name, so renaming or moving the file never breaks a published link.

The response is cacheable by anyone for a day, with an `ETag` that changes when the file is overwritten and a `Last-Modified`; conditional requests are answered `304`, and a `Range` request gets the part asked for (`206`), so a player or a PDF viewer can seek - where the storage provider's stream can seek: the file system's and Azure's can, the S3 and MinIO providers' cannot, and a range asked of them gets the whole file. It never sets a cookie, is marked `nosniff`, and serves a type outside the module's list of known types as a download. `HEAD` is answered without reading the file. A file that is not public is not found, as a missing one is; a cache that kept the response keeps it for up to a day after the file stops being public.

The Blob Management host sets the allowed sizes on its containers with `UseBlobManagementPublicSizes`: on the default container for every container that sets none - ABP's container configuration falls back to the default container - and on a container for that one. A list replaces the one it overrides; each size is from 1 to 4096:

````csharp
Configure<AbpBlobStoringOptions>(options =>
{
    // The host's default, instead of the module's ladder.
    options.Containers.ConfigureDefault(container =>
        container.UseBlobManagementPublicSizes(96, 192, 480, 960));

    // Avatars are shown small: a shorter list, so fewer renditions and fewer cache entries.
    options.Containers.Configure("avatars", container =>
        container.UseBlobManagementPublicSizes(64, 128, 256));
});
````

Or in configuration ([Declaring Containers in Configuration](setting-up.md#declaring-containers-in-configuration)): `BlobManagement:PublicSizes` for the default, and `PublicSizes` in a container.

There is no server-side cache of renditions: a size is rendered again on each request that is not answered by an HTTP cache. A CDN, reverse proxy or rate limit in front of the host is still the host's job - with the query string in the cache key, since the tenant and the size are there; the list of sizes only bounds how many different renditions an address can ask for.

A file that is not public is read in the application and handed on, or by the browser with a [read link](files.md#read-links) the application asks for.

## See Also

* [Keeping Files by Id](files.md)
* [Hosting Modes](hosting.md)
