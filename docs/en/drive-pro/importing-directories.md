```json
//[doc-seo]
{
    "Description": "Import a directory tree into Dignite Drive Pro: DirectoryPathImporter files the unfiled files of a user drive into directories by a path an extra property of each file holds - repeatable, with a dry run by default."
}
```

# Importing a Directory Tree

> Drive Pro has not been released yet; see the [overview](index.md).

Files stored in a user drive before Drive Pro was installed - by the open Drive, or by an application migrating its own files into the Drive - sit at the top of the drive, unfiled. When whoever stored them also wrote down where each belongs, as a path in one of the file's [extra properties](https://abp.io/docs/latest/framework/fundamentals/object-extensions), `DirectoryPathImporter` builds the directories those paths name and files each file in its directory.

The importer knows nothing of who wrote the paths: the operator names the extra property. An application's migration writes, for instance, `Reports/2026/March` into `MyApp:DirectoryPath` of each file it stores in the Drive; the import then reads `MyApp:DirectoryPath`.

## Running an Import

`DirectoryPathImporter` is a domain service in `Dignite.Drive.Pro.Domain`. It is an operator's tool, called from code - a data seed contributor, a migration console, a one-off job - with no API and no permission of its own: it authorizes nobody, and runs in the current tenant and the caller's unit of work.

````csharp
public class FileTreeImport : ITransientDependency
{
    private readonly DirectoryPathImporter _importer;

    public FileTreeImport(DirectoryPathImporter importer)
    {
        _importer = importer;
    }

    [UnitOfWork]
    public virtual async Task<DirectoryPathImportReport> RunAsync(bool dryRun)
    {
        return await _importer.ImportAsync(
            new DirectoryPathImportRequest("shared", "MyApp:DirectoryPath", dryRun));
    }
}
````

`DirectoryPathImportRequest` takes the user drive, the name of the extra property, and `dryRun`, which is `true` unless it is set otherwise; `Separator` is `/` by default.

* **A dry run changes nothing** and reports what an import would do. Run one first and read its report.
* **An import can be run again.** Only files at the top of the drive are read, so a file filed before - by an import or by a user - stays where it is; a directory is looked up before it is created, by its name whatever its case, so an existing one is used and a second run creates nothing the first created.
* **A path is split on the separator**, each name trimmed and empty ones dropped (`/a//b/` is `a/b`). A path deeper than 16 directories, or with a name longer than 256 characters, cannot be filed.
* **In a personal drive** each file's directories are made in its owner's tree; a file without an owner is not filed.
* A file without a path in the property stays at the top of the drive.

## The Report

`DirectoryPathImportReport` says, for an import or for a dry run:

| | |
|---|---|
| `FilesRead` | The unfiled files read. |
| `FilesWithoutPath` | Those without a path in the property. |
| `FilesFiled` | The files filed into a directory, each with its id and the path, as normalized. |
| `DirectoriesCreated` | The directories created, as paths (in a personal drive, the owner first). |
| `DirectoriesReused` | The directories that existed already. |
| `FilesSkipped` | The files with a path that could not be filed, each with the path and the reason. They stay where they are. |

## See Also

* [The File Library](file-library.md)
* [Personal Drives](personal-drives.md)
