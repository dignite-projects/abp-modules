```json
//[doc-seo]
{
    "Description": "Install Dignite Drive in an ABP host: its modules and database, Drive containers declared in code or in appsettings.json, upload presets, allowed clients, public read and the tenant storage quota."
}
```

# Setting Up the Drive

> Drive has not been released yet; see the [overview](index.md).

This page sets up the host that runs the Drive: the application itself when the Drive is in its process, or the Drive's own service. Which of the two is a choice described in [Hosting Modes](hosting.md).

## Installation

Reference `Dignite.Drive.Application`, `Dignite.Drive.HttpApi` and the database provider, `Dignite.Drive.EntityFrameworkCore` or `Dignite.Drive.MongoDB`, and depend on their modules:

````csharp
[DependsOn(
    typeof(DriveApplicationModule),
    typeof(DriveHttpApiModule),
    typeof(DriveEntityFrameworkCoreModule) // or DriveMongoDbModule
)]
public class MyHostModule : AbpModule
{
}
````

`Dignite.Drive.HttpApi` is needed for remote applications, public read and tickets; a host whose only caller is an application in the same process can leave it out.

The Drive's database objects have the `Drive` prefix (the file descriptors are `DriveFileDescriptors`) and use the `Drive` [connection string](https://abp.io/docs/latest/framework/fundamentals/connection-strings), falling back to `Default`. With EF Core, call `ConfigureDrive()` in your migrations `DbContext` and add a migration:

````csharp
protected override void OnModelCreating(ModelBuilder builder)
{
    base.OnModelCreating(builder);

    builder.ConfigureDrive();
}
````

The Drive resizes pictures with ABP's [image manipulation](https://abp.io/docs/latest/framework/infrastructure/image-manipulation) - install one of its providers (`Volo.Abp.Imaging.SkiaSharp`, for example) in this host. Without one, pictures are stored and served as they are.

## Drive Containers

A Drive container is an ABP [BLOB container](https://abp.io/docs/latest/framework/infrastructure/blob-storing) of this host, declared one of the Drive's with `UseDriveContainer`. Its storage provider (`UseFileSystem`, `UseAzure`, ...) and its pipeline contributors are configured on the same container, as usual:

````csharp
Configure<AbpBlobStoringOptions>(options =>
{
    // The storage beneath every Drive container that names none of its own.
    options.Containers.ConfigureDefault(container =>
    {
        container.UseFileSystem(fs => fs.BasePath = "/var/lib/drive");
    });

    options.Containers.Configure("media", container =>
    {
        container.UseDriveContainer(drive =>
        {
            drive.AllowedClientIds = new[] { "site-app", "cloud-app" };
            drive.PublicRead = true;
        });
        container.UseImagesPreset(preset => preset.MaxWidth = 2048);
    });
});
````

`DriveContainerConfiguration` has:

* `Kind`: `System` (the default) for a container applications store in through the provider, or `UserDrive` for the file library.
* `AllowedClientIds`: the OAuth clients - the applications - that may read, write and delete in a system container over HTTP. Empty, the default, allows no remote caller; an application in the same process is trusted and needs no entry. One list covers reading, writing and deleting.
* `PublicRead`: whether anyone may read the container's files at their [public address](provider.md#public-addresses), without signing in. Default: `false`.

`UseDriveContainer` also makes the container multi-tenant: every Drive container keeps each tenant's files apart. Declare named containers only; a default container declared a Drive container would make every container name one, and the host refuses to start.

### Presets

A preset adds the [BLOB Storing](../blob-storing/index.md) contributors a kind of file needs, in the order they have to run. Its parameters are numbers only.

| Preset | What it adds | Defaults |
|---|---|---|
| `UseImagesPreset` | [Size limit](../blob-storing/max-size.md), [allowed types](../blob-storing/allowed-content-types.md) (JPEG, PNG, GIF, WebP), [metadata removal](../blob-storing/imaging.md) and a [resize](../blob-storing/imaging.md) into `MaxWidth` × `MaxHeight` (both `0`: no resize) | 25 MB, 2560 × 4096 |
| `UseDocumentsPreset` | Size limit, allowed types: PDF, Office documents, plain text, CSV and ZIP | 20 MB |
| `UseBinaryAssetsPreset` | Size limit, allowed types: icons and web fonts | 5 MB |
| `UseAnyPreset` | Size limit only: every type is accepted | 20 MB |

````csharp
container.UseDocumentsPreset(preset => preset.MaxSizeInMegabytes = 50);
````

The allowed types leave out SVG, HTML and scripts, which a browser would run on the Drive's own address. `UseAnyPreset` does not: do not combine it with `PublicRead`. (A public read serves a type outside the Drive's list as a download, never inline, but the container should not hold such files to begin with.)

A container can do without a preset, or add contributors after one; calling an `Add…Contributor` method again changes that contributor's configuration.

### Declaring Containers in Configuration

A host with no code of its own for the Drive - the Drive run as its own service - declares its containers in the root-level `Drive` section of `appsettings.json`:

````json
{
  "Drive": {
    "Containers": {
      "media": {
        "Kind": "System",
        "PublicRead": true,
        "AllowedClientIds": [ "site-app", "cloud-app" ],
        "Preset": "Images",
        "PresetOptions": { "MaxSizeInMegabytes": 10, "MaxWidth": 2048 }
      },
      "attachments": {
        "Preset": "Documents"
      }
    }
  }
}
````

Each container is applied with the same methods code uses. `Preset` is one of `Images`, `Documents`, `BinaryAssets` and `Any`, compared without case; `PresetOptions` holds that preset's parameters. The storage provider is not declared here: the containers take the default container's provider and its settings, configured in code as above.

Every mistake stops the host when it starts - an unknown key, kind or preset, a value of the wrong type, a misspelt or invalid preset parameter - rather than on the first upload. Code runs after the section is applied, so a module's or the host's own `Configure<AbpBlobStoringOptions>` overrides it.

To make a preset of your own available by name, add it in a module's `PreConfigureServices`:

````csharp
PreConfigure<DrivePresetOptions>(options =>
{
    options.Presets["Avatars"] = (container, parameters) => container.UseImagesPreset(preset =>
    {
        preset.MaxWidth = 256;
        preset.MaxHeight = 256;
        DrivePresetOptions.Bind(parameters, preset); // PresetOptions from the configuration, if any
    });
});
````

### Startup Checks

When the host starts, the Drive checks its containers once more:

* A default container declared a Drive container stops the host.
* A system container that allows no client is logged as a warning: only callers in the same process can use it.
* An allowed client id the authorization server does not know is logged as a warning - where the host can look clients up, that is where ABP's `IApplicationFinder` is registered (a host with `Volo.Abp.OpenIddict.Domain`, the Drive installed next to its authorization server). A Drive service of its own cannot, and skips the check.

## Tenant Storage

How much a tenant may store is an ABP [feature](https://abp.io/docs/latest/framework/infrastructure/features), set per edition or per tenant with the Feature Management module:

| Feature | Default | |
|---|---|---|
| `Drive.Enable` | `true` | Whether the tenant may store files in the Drive. Turning it off stops storing, not reading what is already public. |
| `Drive.StorageSize` | `0` | The most the tenant may store, in megabytes. `0` is no limit. |

Usage is counted from the file descriptors: each stored BLOB once, however many files share it, across every Drive container; deleted files do not count. New bytes beyond the allocation are refused with `Drive:StorageQuotaExceeded`; the same bytes stored again add nothing and are not refused. The check reads the usage and then stores, so two uploads at the same moment can both pass it: it is a soft limit, exceeded by at most an upload each.

## Errors

| Code | |
|---|---|
| `Drive:ContainerNotFound` | The container is not one of the Drive's (HTTP 404). |
| `Drive:BlobAlreadyExists` | The name is taken and overwriting was not asked for (HTTP 409); the provider turns it into ABP's `BlobAlreadyExistsException`. |
| `Drive:BlobNotFound` | No file of that name (HTTP 404). |
| `Drive:DriveDisabled` | The tenant's `Drive.Enable` is off. |
| `Drive:StorageQuotaExceeded` | The tenant's `Drive.StorageSize` would be exceeded. |
| `Drive:InvalidTicket` | See [Tickets](tickets.md). |

The Drive container's contributors add their own, such as `Dignite.Abp.BlobStoring:ContentTooLarge` and `Dignite.Abp.BlobStoring:ContentTypeNotAllowed`.

## See Also

* [Using the Drive From an Application](provider.md)
* [Hosting Modes](hosting.md)
