```json
//[doc-seo]
{
    "Description": "The Dignite Blob Management MCP tools: blob_* tools an AI assistant uses to list containers, browse directories, upload a file, read, rename, move or delete one, upload links that send a file from the machine it is on, and their options."
}
```

# MCP Tools

> Blob Management has not been released yet; see the [overview](index.md).

`Dignite.Abp.BlobManagement.Mcp` contributes Blob Management's tools to the application's [MCP server](../aspnetcore-mcp/index.md), so an AI assistant can browse the containers and their directories for the signed-in user, add files, and read, rename, move or delete them. Each tool is one call of the container, file or directory services, with the same [permissions](permissions.md) and rules as the HTTP API.

## Installation

Reference `Dignite.Abp.BlobManagement.Mcp` in the host that runs the MCP server, and depend on its module:

````csharp
[DependsOn(typeof(BlobManagementMcpModule))]
public class MyHostModule : AbpModule
{
}
````

The tools call Blob Management's application services, in the same process or remote, as the host installs the module. They are registered in the `blob` namespace: their names start with `blob_`. A server that maps [several endpoints](../aspnetcore-mcp/hosting.md#several-endpoints) names the endpoint that serves `blob`.

## The Tools

| Tool | |
|---|---|
| `blob_list_containers` | The containers the user has something to do in, with the largest file and the extensions each accepts, whether it has directories (`allowDirectories`) and what the user may do at its top (`access`). Where to start. |
| `blob_list_directories` | The directories directly under a directory (`parentId`), or at the top of a container, that the user may see. Each has its id, `hasChildren`, and `access`: what the user may do in it. |
| `blob_create_directory` | Creates a directory (`name`) at the top of a container or under a directory (`parentId`). |
| `blob_list_files` | The files of a directory (`directoryId`), or of the top of a container, by name, with a `filter` on the name and paging (`skipCount`, `maxResultCount`, at most 100 at a time). Each has its id, `url` in a public container, `directoryId` and `isAddressedByName`. |
| `blob_get_file` | One file's details, by its id. |
| `blob_create_upload_url` | A one-time link to send a file (`fileName`) to, into a directory (`directoryId`) or the top of the container, and the command that sends it. |
| `blob_upload_file` | Adds a small file from base64 (`contentBase64`, a `data:` URL accepted), into a directory (`directoryId`) or the top of the container. |
| `blob_update_file` | Renames a file (`name`), moves it to another directory of the same container (`directoryId`) or to the top (`moveToTop`), or both. Only what is passed changes. A file with `isAddressedByName` is neither renamed nor moved. |
| `blob_delete_file` | Deletes a file by its id (marked destructive). |

Every tool that names a container takes it as `containerName`, from `blob_list_containers`. Renaming, moving and deleting directories, and their permissions, are left to the [management page](angular-ui.md#the-management-page): a directory carries its files with it, a larger change than an assistant should make in passing.

`blob_update_file` checks the new name first and moves before it renames, so that a refused move leaves the file as it was.

## Sending a File

An assistant would have to write a file's bytes out as text to pass them in a tool call, so a real file goes by a link: `blob_create_upload_url` returns a one-time [upload link](attaching-files.md#upload-links-for-users), good for ten minutes, and the commands that post the file to it from the machine it is on - `command`, with `curl -F "content=@FILE_PATH" …`, for sh, bash, zsh and cmd.exe, and `powerShellCommand`, with `curl.exe`, for PowerShell. The bytes never pass through the assistant, and the upload is authorized again, as the user, when it arrives.

`blob_upload_file` takes the bytes as base64 and suits only a file of a few kilobytes.

## Options

`BlobManagementMcpOptions`:

| Option | Default | |
|---|---|---|
| `BaseUrl` | `null` | Blob Management's address, which upload links start with. `null` uses the address the MCP request came in on: right when the MCP server runs in Blob Management's host. Set it when Blob Management is its own service. |
| `MaxInlineUploadSize` | 2 MB | The largest file `blob_upload_file` takes; the container's own limit applies too. It fits the MCP endpoint's default body limit (`AbpMcpServerOptions.MaxRequestBodySize`, 4 MB) - raise that as well to raise this. |

````csharp
Configure<BlobManagementMcpOptions>(options =>
{
    options.BaseUrl = "https://blobs.example.com";
});
````

## See Also

* [Attaching Files](attaching-files.md)
* [Directories](directories.md)
* [MCP Server](../aspnetcore-mcp/index.md)
