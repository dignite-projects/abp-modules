```json
//[doc-seo]
{
    "Description": "Import a directory tree into Dignite Blob Management: DirectoryPathImporter files the unfiled files of a container into directories by a path an extra property of each file holds - repeatable, with a dry run by default."
}
```

# Importing a Directory Tree

> Blob Management has not been released yet; see the [overview](index.md).

Files stored by id in a container before it [allowed directories](directories.md) - or stored by an application that migrates a file library of its own in - sit at the top of the container, unfiled. When whoever stored them also wrote down where each belongs, as a path in one of the file's [extra properties](https://abp.io/docs/latest/framework/fundamentals/object-extensions), `DirectoryPathImporter` builds the directories those paths name and files each file in its directory.

The importer knows nothing of who wrote the paths: the operator names the extra property. An application's migration writes, for instance, `Reports/2026/March` into `MyApp:DirectoryPath` of each file it stores; the import then reads `MyApp:DirectoryPath`.

## Running an Import

`DirectoryPathImporter` is a domain service in `Dignite.Abp.BlobManagement.Domain`. It is an operator's tool, called from code - a data seed contributor, a migration console, a one-off job - with no API and no permission of its own: it authorizes nobody, and runs in the current tenant and the caller's unit of work. The container must allow directories.

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
            new DirectoryPathImportRequest("documents", "MyApp:DirectoryPath", dryRun));
    }
}
````

`DirectoryPathImportRequest` takes the container, the name of the extra property, and `dryRun`, which is `true` unless it is set otherwise; `Separator` is `/` by default.

* **A dry run changes nothing** and reports what an import would do. Run one first and read its report.
* **An import can be run again.** Only files at the top of the container are read, so a file filed before - by an import or by its business system - stays where it is; a directory is looked up before it is created, by its name whatever its case, so an existing one is used and a second run creates nothing the first created.
* **Only files kept by id are read.** A file an application saved under its name stays at the top of its container.
* **A path is split on the separator**, each name trimmed and empty ones dropped (`/a//b/` is `a/b`). A path deeper than 16 directories, or with a name longer than 256 characters, cannot be filed.
* A file without a path in the property stays at the top of the container.

## The Report

`DirectoryPathImportReport` says, for an import or for a dry run:

| | |
|---|---|
| `FilesRead` | The unfiled files read. |
| `FilesWithoutPath` | Those without a path in the property. |
| `FilesFiled` | The files filed into a directory, each with its id and the path, as normalized. |
| `DirectoriesCreated` | The directories created, as paths from the top of the container. |
| `DirectoriesReused` | The directories that existed already. |
| `FilesSkipped` | The files with a path that could not be filed, each with the path and the reason. They stay where they are. |

## See Also

* [Directories](directories.md)
* [Setting Up](setting-up.md)
