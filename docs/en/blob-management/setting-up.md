```json
//[doc-seo]
{
    "Description": "Install Dignite Blob Management in an ABP host: its modules and database, managed containers declared in code or in appsettings.json, directories, upload presets, allowed clients, public read and the tenant storage allocation."
}
```

# Setting Up

> Blob Management has not been released yet; see the [overview](index.md).

This page sets up the host that runs Blob Management: the application itself when the module is in its process, or Blob Management's own service. Which of the two is a choice described in [Hosting Modes](hosting.md).

## Installation

Reference `Dignite.Abp.BlobManagement.Application`, `Dignite.Abp.BlobManagement.HttpApi` and the database provider, `Dignite.Abp.BlobManagement.EntityFrameworkCore` or `Dignite.Abp.BlobManagement.MongoDB`, and depend on their modules:

````csharp
[DependsOn(
    typeof(BlobManagementApplicationModule),
    typeof(BlobManagementHttpApiModule),
    typeof(BlobManagementEntityFrameworkCoreModule) // or BlobManagementMongoDbModule
)]
public class MyHostModule : AbpModule
{
}
````

`Dignite.Abp.BlobManagement.HttpApi` is needed for remote applications, public read, the directory and usage APIs and the management page; a host whose only caller is an application in the same process can leave it out.

The database objects have the `Blob` prefix (`BlobFileDescriptors`, `BlobDirectoryDescriptors`) and use the `BlobManagement` [connection string](https://abp.io/docs/latest/framework/fundamentals/connection-strings), falling back to `Default`. With EF Core, call `ConfigureBlobManagement()` in your migrations `DbContext` and add a migration:

````csharp
protected override void OnModelCreating(ModelBuilder builder)
{
    base.OnModelCreating(builder);

    builder.ConfigureBlobManagement();
}
````

With MongoDB, `BlobManagementMongoDbModule` keeps the files and directories in the `BlobFileDescriptors` and `BlobDirectoryDescriptors` collections.

Blob Management processes pictures with SkiaSharp, which comes with it: the [images preset](#presets) stores a JPEG as WebP and resizes through ABP's [image manipulation](https://abp.io/docs/latest/framework/infrastructure/image-manipulation) SkiaSharp provider, and public reads resize on request with it. The domain module depends on the [SkiaSharp image contributors](../blob-storing/imaging-skiasharp.md) module, which also makes ABP's SkiaSharp resizer turn photos upright and changes its default sampling and quality for the whole host. SkiaSharp brings its native libraries for Windows and macOS only: **on Linux, reference `SkiaSharp.NativeAssets.Linux`** in this host, or every picture fails to process.

## Managed Containers

A managed container is an ABP [BLOB container](https://abp.io/docs/latest/framework/infrastructure/blob-storing) of this host, declared one with `UseBlobManagementContainer`. Its storage provider (`UseFileSystem`, `UseAzure`, ...) and its pipeline contributors are configured on the same container, as usual:

````csharp
Configure<AbpBlobStoringOptions>(options =>
{
    // The storage beneath every managed container that names none of its own.
    options.Containers.ConfigureDefault(container =>
    {
        container.UseFileSystem(fs => fs.BasePath = "/var/lib/blobs");
    });

    options.Containers.Configure("media", container =>
    {
        container.UseBlobManagementContainer(managed =>
        {
            managed.AllowedClientIds = new[] { "site-app", "cloud-app" };
            managed.PublicRead = true;
        });
        container.UseImagesPreset(preset => preset.MaxWidth = 2048);
    });

    options.Containers.Configure("documents", container =>
    {
        container.UseBlobManagementContainer(managed => managed.AllowDirectories = true);
        container.UseDocumentsPreset();
    });
});
````

There is one kind of managed container: applications store in it by name through the [provider](provider.md) and by id through the [file service](files.md), and the management page shows both. `BlobManagementContainerConfiguration` has:

* `AllowedClientIds`: the OAuth clients - the applications - that may read, write and delete in the container through the BLOB API and the file API. Empty, the default, allows no remote caller; an application in the same process is trusted and needs no entry. One list covers reading, writing and deleting.
* `PublicRead`: whether anyone may read the container's files at their [public address](provider.md#public-addresses), without signing in. Default: `false`.
* `AllowDirectories`: whether the container has a directory tree for its files addressed by id ([Directories](directories.md)). Default: `false` - the container's files are one flat list, and every directory operation is refused (`BlobManagement:DirectoriesNotAllowed`).

`UseBlobManagementContainer` also makes the container multi-tenant: every managed container keeps each tenant's files apart. Declare named containers only; a default container declared managed would make every container name one, and the host refuses to start.

### Presets

A preset adds the [BLOB Storing](../blob-storing/index.md) contributors a kind of file needs, in the order they have to run. Its parameters are numbers only.

| Preset | What it adds | Defaults |
|---|---|---|
| `UseImagesPreset` | [Size limit](../blob-storing/max-size.md), [allowed types](../blob-storing/allowed-content-types.md) (JPEG, PNG, GIF, WebP), [metadata removal](../blob-storing/imaging.md#image-metadata), [a JPEG stored as WebP](../blob-storing/imaging-skiasharp.md#storing-jpegs-as-webp) (`StoreJpegAsWebp`, at `WebpQuality`) and a [resize](../blob-storing/imaging.md#image-resize) into `MaxWidth` × `MaxHeight` (both `0`: no resize) | 25 MB, 2560 × 4096, WebP on at quality 85 |
| `UseDocumentsPreset` | Size limit, allowed types: PDF, Office documents, plain text, CSV and ZIP | 20 MB |
| `UseBinaryAssetsPreset` | Size limit, allowed types: icons and web fonts | 5 MB |
| `UseAnyPreset` | Size limit only: every type is accepted | 20 MB |

````csharp
container.UseDocumentsPreset(preset => preset.MaxSizeInMegabytes = 50);
````

The allowed types leave out SVG, HTML and scripts, which a browser would run on the host's own address. `UseAnyPreset` does not: do not combine it with `PublicRead`. (A public read serves a type outside the module's list as a download, never inline, but the container should not hold such files to begin with.)

A container can do without a preset, or add contributors after one; calling an `Add…Contributor` method again changes that contributor's configuration.

#### Pictures Stored as WebP

With `StoreJpegAsWebp` on (the default), the images preset stores every JPEG as a lossy WebP at `WebpQuality` (1-100, default 85): turned upright by its EXIF orientation, so a phone's portrait photo is not shown on its side, and fitted into `MaxWidth` × `MaxHeight` in the same decode. A PNG keeps its pixels, and a GIF and a WebP are stored as they are; a PNG or WebP larger than the box is resized, upright.

The physical BLOB of a converted JPEG is named `.webp`, and the file records `image/webp` with the size of the WebP. A file stored by id takes the `.webp` extension in its name too (`holiday.jpg` becomes `holiday.webp`), so a download is named for what it is. A file an application saves keeps its name, which is its address: an application that stored `covers/holiday.jpg` reads it back by that name, and gets the WebP. Uploading the same JPEG again shares the stored WebP.

Turn it off for a container that must keep JPEGs as they are, in code or in [configuration](#declaring-containers-in-configuration):

````csharp
container.UseImagesPreset(preset => preset.StoreJpegAsWebp = false);
````

````json
"PresetOptions": { "StoreJpegAsWebp": false }
````

Changing it does not touch what is stored: JPEGs stored as WebP stay WebP, and with it off, new JPEGs are stored as JPEGs.

### Declaring Containers in Configuration

A host with no code of its own for Blob Management - the module run as its own service - declares its containers in the root-level `BlobManagement` section of `appsettings.json`:

````json
{
  "BlobManagement": {
    "Containers": {
      "media": {
        "PublicRead": true,
        "AllowedClientIds": [ "site-app", "cloud-app" ],
        "Preset": "Images",
        "PresetOptions": { "MaxSizeInMegabytes": 10, "MaxWidth": 2048 }
      },
      "documents": {
        "AllowDirectories": true,
        "Preset": "Documents"
      }
    }
  }
}
````

Each container is applied with the same methods code uses. The keys are `PublicRead`, `AllowDirectories`, `AllowedClientIds`, `Preset` and `PresetOptions`. `Preset` is one of `Images`, `Documents`, `BinaryAssets` and `Any`, compared without case; `PresetOptions` holds that preset's parameters. The storage provider is not declared here: the containers take the default container's provider and its settings, configured in code as above.

Every mistake stops the host when it starts - an unknown key or preset, a value of the wrong type, `PresetOptions` without a `Preset`, a misspelt or invalid preset parameter - rather than on the first upload. Code runs after the section is applied, so a module's or the host's own `Configure<AbpBlobStoringOptions>` overrides it.

To make a preset of your own available by name, add it in a module's `PreConfigureServices`:

````csharp
PreConfigure<BlobManagementPresetOptions>(options =>
{
    options.Presets["Avatars"] = (container, parameters) => container.UseImagesPreset(preset =>
    {
        preset.MaxWidth = 256;
        preset.MaxHeight = 256;
        BlobManagementPresetOptions.Bind(parameters, preset); // PresetOptions from the configuration, if any
    });
});
````

### Startup Checks

When the host starts, Blob Management checks its containers once more:

* A default container declared managed stops the host.
* An allowed client id the authorization server does not know is logged as a warning - where the host can look clients up, that is where ABP's `IApplicationFinder` is registered (a host with `Volo.Abp.OpenIddict.Domain`, Blob Management installed next to its authorization server). A Blob Management service of its own cannot, and skips the check.

## Tenant Storage

How much a tenant may store is an ABP [feature](https://abp.io/docs/latest/framework/infrastructure/features), set per edition or per tenant with the Feature Management module:

| Feature | Default | |
|---|---|---|
| `BlobManagement.Enable` | `true` | Whether the tenant may store files. Turning it off stops storing, and closes the file, directory, container and usage services; it does not close reading what is already public. |
| `BlobManagement.StorageSize` | `0` | The most the tenant may store, in megabytes. `0` is no limit. |

Usage is counted from the file descriptors: each stored BLOB once, however many files share it, across every managed container; deleted files do not count. New bytes beyond the allocation are refused with `BlobManagement:StorageQuotaExceeded`; the same bytes stored again add nothing and are not refused. The check reads the usage and then stores, so two uploads at the same moment can both pass it: it is a soft limit, exceeded by at most an upload each. The allocation is the tenant's only: there is none for a container or a user. What is used is shown on the [Usage](usage.md) page.

## Errors

| Code | |
|---|---|
| `BlobManagement:ContainerNotFound` | The container is not a managed one (HTTP 404). |
| `BlobManagement:BlobAlreadyExists` | The name is taken and overwriting was not asked for (HTTP 409); the provider turns it into ABP's `BlobAlreadyExistsException`. |
| `BlobManagement:BlobNotFound` | No file of that name (HTTP 404). |
| `BlobManagement:BlobManagementDisabled` | The tenant's `BlobManagement.Enable` is off. |
| `BlobManagement:StorageQuotaExceeded` | The tenant's `BlobManagement.StorageSize` would be exceeded. |
| `BlobManagement:FileAddressedByName` | The file was saved by an application under its name, and is not renamed or moved (HTTP 409). |
| `BlobManagement:DirectoriesNotAllowed` and the other directory errors | See [Directories](directories.md#errors). |

The container's contributors add their own, such as `Dignite.Abp.BlobStoring:ContentTooLarge` and `Dignite.Abp.BlobStoring:ContentTypeNotAllowed`.

## See Also

* [Using It From an Application](provider.md)
* [Keeping Files by Id](files.md)
* [Directories](directories.md)
* [Usage](usage.md)
* [Hosting Modes](hosting.md)
