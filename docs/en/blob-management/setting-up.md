```json
//[doc-seo]
{
    "Description": "Install Dignite Blob Management in an ABP host: its modules and database, managed containers declared in code or in appsettings.json, directories, upload presets and the tenant storage allocation."
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
        container.UseBlobManagementContainer();
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

* `AllowDirectories`: whether the container has a directory tree for its files addressed by id ([Directories](directories.md)). Default: `false` - the container's files are one flat list, and every directory operation is refused (`BlobManagement:DirectoriesNotAllowed`).

Which applications may use a container over HTTP is not configured here: an OAuth client is granted the container's files in ABP's permission management ([Granting an Application](permissions.md#granting-an-application)). An application in the same process is trusted and needs no grant.

Nor is whether a file is read publicly: that is the business system's call, made on each file - by the application's own container for the BLOBs it saves by name ([Public Addresses](provider.md#public-addresses)), and when it stores a file by id or later ([Public Files](files.md#public-files)).

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

The allowed types leave out SVG, HTML and scripts, which a browser would run on the host's own address. `UseAnyPreset` does not: a public read or a read link serves a type outside the module's list as a download, never inline, but a container whose files are read by browsers should not hold such files to begin with.

A container can do without a preset, or add contributors after one; calling an `Add…Contributor` method again changes that contributor's configuration.

#### Pictures Stored as WebP

With `StoreJpegAsWebp` on (the default), the images preset stores every JPEG as a lossy WebP at `WebpQuality` (1-100, default 85): turned upright by its EXIF orientation, so a phone's portrait photo is not shown on its side, and fitted into `MaxWidth` × `MaxHeight` in the same decode. A PNG keeps its pixels, and a GIF and a WebP are stored as they are; a PNG or WebP larger than the box is resized, upright.

The physical BLOB of a converted JPEG is named `.webp`, and the file records `image/webp` with the size of the WebP. A file stored by id takes the `.webp` extension in its name too (`holiday.jpg` becomes `holiday.webp`), so a download is named for what it is. A file an application saves keeps its name, which is its address: an application that stored `covers/holiday.jpg` reads it back by that name, and gets the WebP.

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
        "Preset": "Images",
        "PresetOptions": { "MaxSizeInMegabytes": 10, "MaxWidth": 2048 }
      },
      "documents": {
        "AllowDirectories": true,
        "Preset": "Documents"
      },
      "avatars": {
        "Preset": "Images",
        "PublicSizes": [ 64, 128, 256 ]
      }
    },
    "PublicSizes": [ 128, 256, 512, 1024 ]
  }
}
````

Each container is applied with the same methods code uses. The keys are `AllowDirectories`, `Preset`, `PresetOptions` and `PublicSizes`. `Preset` is one of `Images`, `Documents`, `BinaryAssets` and `Any`, compared without case; `PresetOptions` holds that preset's parameters. `PublicSizes` is the list of widths and heights a [public read](provider.md#public-addresses) of the container's pictures may ask for; `PublicSizes` at the root of the section is the host's default for every container without one. The storage provider is not declared here: the containers take the default container's provider and its settings, configured in code as above.

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

When the host starts, Blob Management checks its containers once more: a default container declared managed stops the host.

## Tenant Storage

How much a tenant may store is an ABP [feature](https://abp.io/docs/latest/framework/infrastructure/features), set per edition or per tenant with the Feature Management module:

| Feature | Default | |
|---|---|---|
| `BlobManagement.Enable` | `true` | Whether the tenant may store files. Turning it off stops storing, and closes the file, directory, container and usage services; it does not close reading what is already public, or a read link already handed out. |
| `BlobManagement.StorageSize` | `0` | The most the tenant may store, in megabytes. `0` is no limit. |

Usage is counted from the file descriptors: the size of every file, across every managed container - each file has a BLOB of its own, so the same bytes in two files count twice; deleted files do not count, and files uploaded with a ticket and not confirmed yet do. Bytes beyond the allocation are refused with `BlobManagement:StorageQuotaExceeded`. The check reads the usage and then stores, so two uploads at the same moment can both pass it: it is a soft limit, exceeded by at most an upload each. The allocation is the tenant's only: there is none for a container or a user. What is used is shown on the [Usage](usage.md) page.

## Errors

| Code | |
|---|---|
| `BlobManagement:ContainerNotFound` | The container is not a managed one (HTTP 404). |
| `BlobManagement:BlobAlreadyExists` | The name is taken and overwriting was not asked for (HTTP 409); the provider turns it into ABP's `BlobAlreadyExistsException`. |
| `BlobManagement:BlobNotFound` | No file of that name (HTTP 404). |
| `BlobManagement:BlobManagementDisabled` | The tenant's `BlobManagement.Enable` is off. |
| `BlobManagement:StorageQuotaExceeded` | The tenant's `BlobManagement.StorageSize` would be exceeded (HTTP 507). |
| `BlobManagement:FileAddressedByName` | The file was saved by an application under its name, and is not renamed, moved or marked public by id (HTTP 409). |
| `BlobManagement:UploadTicketInvalid` | An upload ticket that is expired, altered or not this Blob Management's (HTTP 403). |
| `BlobManagement:UploadTicketUsed` | The ticket's file was stored already: a ticket stores one file, once (HTTP 409). |
| `BlobManagement:FilePending` | The file was uploaded with a ticket and is not confirmed yet, so it gets no read link (HTTP 409). |
| `BlobManagement:ReadLinkInvalid` | A read link that is expired, altered or not this Blob Management's (HTTP 403). |
| `BlobManagement:DirectoriesNotAllowed` and the other directory errors | See [Directories](directories.md#errors). |

The container's contributors add their own, which Blob Management's HTTP API answers with the status of what is wrong with the content:

| Code | HTTP |
|---|---|
| `Dignite.Abp.BlobStoring:ContentTooLarge` | 413 |
| `Dignite.Abp.BlobStoring:ContentTypeNotAllowed` | 415 |
| `Dignite.Abp.BlobStoring:ContentTypeMismatch` | 400 |
| `Dignite.Abp.BlobStoring.Imaging:ImageTooLarge`, `ImageTooSmall` | 400 |
| `Dignite.Abp.BlobStoring.Imaging:ImageProcessingFailed`, `ImageDecodeTimeout` | 422 |

## See Also

* [Using It From an Application](provider.md)
* [Keeping Files by Id](files.md)
* [Directories](directories.md)
* [Usage](usage.md)
* [Hosting Modes](hosting.md)
