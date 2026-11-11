```json
//[doc-seo]
{
    "Description": "Set up Dignite Drive Pro in the host that runs the Drive: its packages and modules, its directory table and the columns it adds to the Drive's file table, design-time migrations, MongoDB, and what changes for the open Drive's callers."
}
```

# Setting Up Drive Pro

> Drive Pro has not been released yet; see the [overview](index.md).

Drive Pro is installed in the host that runs the [Drive](../drive/drive-host.md), beside it: the Drive is set up as usual - its containers, presets and tenant quota - and Drive Pro adds to it.

## Installation

Reference `Dignite.Drive.Pro.Application`, `Dignite.Drive.Pro.HttpApi` and the database provider, `Dignite.Drive.Pro.EntityFrameworkCore` or `Dignite.Drive.Pro.MongoDB`, and depend on their modules. Each depends on its open counterpart, so these replace the open modules in the list:

````csharp
[DependsOn(
    typeof(DriveProApplicationModule),
    typeof(DriveProHttpApiModule),
    typeof(DriveProEntityFrameworkCoreModule) // or DriveProMongoDbModule
)]
public class MyHostModule : AbpModule
{
}
````

The packages come from GitHub Packages. A consuming solution adds the feed to its `NuGet.Config` with a token in an environment variable, never in the file:

````xml
<packageSources>
  <add key="dignite-projects" value="https://nuget.pkg.github.com/dignite-projects/index.json" />
</packageSources>
<packageSourceCredentials>
  <dignite-projects>
    <add key="Username" value="dignite-projects" />
    <add key="ClearTextPassword" value="%GITHUB_PACKAGES_TOKEN%" />
  </dignite-projects>
</packageSourceCredentials>
````

An application that reaches a [remote Drive](../drive/hosting.md#the-drive-as-its-own-service) through C# references `Dignite.Drive.Pro.HttpApi.Client` and depends on `DriveProHttpApiClientModule`; the proxies use the remote service `Drive`, as the open ones do.

## The Database

Drive Pro is in the Drive's database: its directory table has the Drive's prefix (`DriveDirectoryDescriptors`) and uses the `Drive` connection string, and it adds two columns to the Drive's file table, `DriveProDirectoryId` and `DriveProOwnerId`, with the indexes that list a directory.

### Entity Framework Core

Call `ConfigureDrivePro()` after `ConfigureDrive()` in your migrations `DbContext`:

````csharp
protected override void OnModelCreating(ModelBuilder builder)
{
    base.OnModelCreating(builder);

    builder.ConfigureDrive();
    builder.ConfigureDrivePro();
}
````

The two columns on the file table are [object extensions](https://abp.io/docs/latest/framework/fundamentals/object-extensions) of the Drive's file descriptor, mapped to columns. The module maps them when the application starts, but `dotnet ef migrations add` does not start the application: **the design-time factory must call `DriveProEfCoreEntityExtensionMappings.Configure()`** before it creates the context, or the migration leaves the columns out. In ABP's templates the place is the `EfCoreEntityExtensionMappings` class the factory already calls:

````csharp
public static class MyEfCoreEntityExtensionMappings
{
    public static void Configure()
    {
        DriveProEfCoreEntityExtensionMappings.Configure();
        // ...
    }
}
````

Then add a migration.

### MongoDB

`DriveProMongoDbModule` keeps the directories in their own collection, `DriveDirectoryDescriptors`, and adds its indexes to the Drive's file collection; the two fields are top-level fields of a file's document. The file collection's indexes are added by replacing ABP's `IMongoModelSource`, which keeps the open Drive's indexes and adds Drive Pro's. An application that replaces `IMongoModelSource` itself replaces Drive Pro's too: the last registration wins.

## Personal Drives

A user drive holds one tree for the whole tenant unless it is declared **personal**, with a tree for each user. That is set on the Drive container, in code or in Drive Pro's own section of the configuration; see [Personal Drives](personal-drives.md).

## What Changes for the Open Drive's Callers

The open file API keeps its routes, contracts and behaviour, with Drive Pro's [permissions](permissions.md) in place of the open rule:

* **Reading a file by its id needs permission to view it.** An application that reads files of a user drive by their ids - an article showing its attachments, say - needs its users to hold `Drive.Files.View`, a grant on the files, or to have uploaded them. Granting `Drive.Files.View` to the role every user has restores the open behaviour in the tenant-wide drives ([Reading a File by Its Id](permissions.md#reading-a-file-by-its-id)).
* **An upload goes to the top of the drive**, as in the open Drive, and needs `Drive.Files.Upload` or a grant on the drive. In a personal drive it goes to the top of the uploader's own tree. Drive Pro's own upload files into a directory ([The File Library](file-library.md#uploading-into-a-directory)).
* **The uploader may still delete their own file.** An application that deletes the files a form no longer uses, as the open Drive's file input recommends, keeps working.
* **Public reads are unchanged:** a file of a `PublicRead` drive is read by anyone at its address.
* `DriveFileDto` and `DriveContainerDto` carry Drive Pro's fields in their `extraProperties` ([The File Library](file-library.md#what-a-file-carries)).

## Errors

Drive Pro's errors are in the Drive's namespace, `Drive:`, and their texts in the Drive's localization resource:

| Code | |
|---|---|
| `Drive:DirectoryNotFound` | The directory does not exist where it was named - in another drive, or in another user's personal drive. Which of them is not told. |
| `Drive:DirectoryNameAlreadyExists` | A directory beside it has the name already, compared without regard to case. |
| `Drive:DirectoryCannotMoveIntoItself` | A directory cannot be moved into itself or below itself. |
| `Drive:DirectoryTooDeep` | The tree would be deeper than 16 directories. |
| `Drive:DirectoryNotEmpty` | Only an empty directory can be deleted. |
| `Drive:SortingNotSupported` | A file listing was asked to sort by something it does not sort by. |

A directory or file that does not exist - or is not in a user drive, or not in the drive the call names - is ABP's not found (404); an action the caller may not take, reading included, ABP's authorization error (403).

## See Also

* [Setting Up the Drive](../drive/drive-host.md)
* [Permissions](permissions.md)
