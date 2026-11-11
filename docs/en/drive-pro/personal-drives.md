```json
//[doc-seo]
{
    "Description": "Dignite Drive Pro personal drives: a user drive in which every user has a directory tree of their own, declared with SetDriveScope or in the DrivePro configuration section."
}
```

# Personal Drives

> Drive Pro has not been released yet; see the [overview](index.md).

A user drive holds **one tree for the whole tenant** by default: everyone who may use it works in the same directories. Declared **personal**, it holds **one tree for each user** instead - "My files" beside the shared drives.

## What Changes in a Personal Drive

* **Each user's files and directories are theirs.** Every file and directory in a personal drive has an owner, the user whose tree holds it, and the owner may do anything with them.
* **The top of the drive is the caller's own.** Listing, creating a directory or uploading without a directory means the top of the caller's tree, and the open upload and upload links put a file there too.
* **A file never changes owners.** It moves within its owner's tree only.
* **Module-wide permissions reach the user's own items only.** `Drive.Files.View`, `Drive.Directories.View` and the others act on every item of a tenant-wide drive, but in a personal drive on the caller's own: nobody browses another user's drive with them. Another user's directory or file is reached through a [grant](permissions.md#per-item-grants) on it - given by a holder of `Drive.ManagePermissions` - and nothing else.
* **Putting things at the top still takes a permission.** The top of a drive is nobody's item, so a user needs `Drive.Files.Upload` (or a grant on the drive) to upload there, and `Drive.Directories.Create` (or a grant) to create a directory there - in their own drive as anywhere. Inside their own directories they need nothing more.

## Declaring One

The drive is declared as a user drive the usual way ([Drive Containers](../drive/drive-host.md#drive-containers)), and Drive Pro's **scope** is set on it. In code, after `UseDriveContainer`:

````csharp
Configure<AbpBlobStoringOptions>(options =>
{
    options.Containers.Configure("my-files", container =>
    {
        container.UseDriveContainer(drive => drive.Kind = DriveContainerKind.UserDrive);
        container.SetDriveScope(DriveContainerScope.Personal);
        container.UseAnyPreset();
    });
});
````

Or in configuration, in Drive Pro's own `DrivePro` section beside the Drive's `Drive` section (the Drive's own section refuses keys it does not know):

````json
{
  "Drive": {
    "Containers": {
      "my-files": { "Kind": "UserDrive", "Preset": "Any" },
      "shared":   { "Kind": "UserDrive", "Preset": "Documents" }
    }
  },
  "DrivePro": {
    "Containers": {
      "my-files": { "Scope": "Personal" }
    }
  }
}
````

`Scope` is `Tenant` (the default) or `Personal`. A mistake stops the host at startup: an unknown key or scope in the `DrivePro` section, or a scope on a container that is not a user drive.

**Choose the scope before the drive holds files.** A file's owner is set when it is stored, and is never changed afterwards: files stored while the drive was tenant-wide have no owner, and declaring the drive personal later does not give them one.

## See Also

* [The File Library](file-library.md)
* [Permissions](permissions.md)
* [Setting Up the Drive](../drive/drive-host.md)
