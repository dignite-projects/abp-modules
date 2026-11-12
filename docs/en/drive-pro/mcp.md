```json
//[doc-seo]
{
    "Description": "The Dignite Drive Pro MCP tools: drive_list_directories, drive_list_files, drive_create_directory and drive_update_file, added to the open Drive's drive_* tools so an AI assistant browses the drives and files what it uploads."
}
```

# MCP Tools

> Drive Pro has not been released yet; see the [overview](index.md).

`Dignite.Drive.Pro.Mcp` adds the [file library](file-library.md) to the Drive's tools on the application's [MCP server](../aspnetcore-mcp/index.md): an AI assistant browses a drive a directory at a time, creates directories, and renames and moves files - those it uploads among them. Each tool is one call of the library's services, with the same [permissions](permissions.md) and rules as the HTTP API.

## Installation

Reference `Dignite.Drive.Pro.Mcp` in the host that runs the MCP server, and depend on its module in place of the open `DriveMcpModule`, which comes with it:

````csharp
[DependsOn(typeof(DriveProMcpModule))]
public class MyHostModule : AbpModule
{
}
````

Drive Pro's tools join the open Drive's in the same `drive` namespace, with the open module's options ([MCP Tools](../drive/mcp.md)): the open tools - `drive_list_containers`, `drive_create_upload_url`, `drive_upload_file`, `drive_get_file`, `drive_delete_file` - keep their names and parameters, now under Drive Pro's rules.

## The Tools

| Tool | |
|---|---|
| `drive_list_directories` | The directories directly under a directory, or at the top of a drive, that the user may see. Each has its id, `hasChildren`, and whether the user may upload (`canUpload`) or create directories (`canCreateDirectory`) in it. |
| `drive_list_files` | The files of a directory, or of the top of a drive, by name, with a filter on the name and paging (at most 100 at a time). Each has, in `extraProperties`, its directory and whether the user may rename, move or delete it. |
| `drive_create_directory` | Creates a directory at the top of a drive or under a directory. |
| `drive_update_file` | Renames a file, moves it to another directory of the same drive (`directoryId`) or to the top (`moveToTop`), or both. Only what is passed changes. |

Without a directory, each tool means the top of the drive - in a [personal drive](personal-drives.md), the top of the user's own.

## Filing an Upload

There is no tool that uploads into a directory. The open upload tools - `drive_upload_file` and the upload link of `drive_create_upload_url` - put a file at the top of the drive, and the assistant then moves it with `drive_update_file`: two calls. The server's instructions tell the assistant so. If the move is refused, the file stays at the top of the drive, where the user who uploaded it sees it and may delete it.

`drive_update_file` moves before it renames, and checks the new name first, so that a call refused half way leaves the file where it was rather than renamed and unmoved.

## See Also

* [The File Library](file-library.md)
* [MCP Tools of the open Drive](../drive/mcp.md)
* [MCP Server](../aspnetcore-mcp/index.md)
