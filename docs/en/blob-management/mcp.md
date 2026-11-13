```json
//[doc-seo]
{
    "Description": "The Dignite Blob Management MCP tools: read-only blob_* tools an AI assistant uses to list containers, browse directories, list files and read one file's details, for a user who holds the management permission."
}
```

# MCP Tools

> Blob Management has not been released yet; see the [overview](index.md).

`Dignite.Abp.BlobManagement.Mcp` contributes Blob Management's tools to the application's [MCP server](../aspnetcore-mcp/index.md), so an AI assistant can look at what the containers hold, for the signed-in user: what the [management page](angular-ui.md#the-management-page) shows. **Every tool reads.** A file is stored, renamed, moved and deleted by the business system that keeps it, with that system's own tools.

## Installation

Reference `Dignite.Abp.BlobManagement.Mcp` in the host that runs the MCP server, and depend on its module:

````csharp
[DependsOn(typeof(BlobManagementMcpModule))]
public class MyHostModule : AbpModule
{
}
````

The tools call Blob Management's container, directory and file application services, with the same [permissions](permissions.md) and rules as the HTTP API. The file tools use `IBlobManagementFileAppService`, which `Dignite.Abp.BlobManagement.Application` provides: install the MCP module in the host that runs Blob Management. The tools are registered in the `blob` namespace: their names start with `blob_`. A server that maps [several endpoints](../aspnetcore-mcp/hosting.md#several-endpoints) names the endpoint that serves `blob`.

## The Tools

| Tool | |
|---|---|
| `blob_list_containers` | The containers the user has something to do in, with the largest file and the extensions each accepts, whether it has directories (`allowDirectories`) and what the user may do with its directories (`access`). Where to start. |
| `blob_list_directories` | The directories directly under a directory (`parentId`), or at the top of a container. Each has its id and `hasChildren`. Needs seeing the container's directory tree. |
| `blob_list_files` | The files of a directory (`directoryId`), or of the top of a container, by name, with a `filter` on the name and paging (`skipCount`, `maxResultCount`, at most 100 at a time). Each has its id, size, type, `url` in a public container, `directoryId`, `creatorId`, `clientId` and `isAddressedByName`. Needs `BlobManagement.Containers`. |
| `blob_get_file` | One file's details, by its id. Needs `BlobManagement.Containers`. |

Every tool that names a container takes it as `containerName`, from `blob_list_containers`. Directories are created, renamed, moved and deleted on the [management page](angular-ui.md#the-management-page) or through the [directory API](directories.md#the-api), not by an assistant.

## See Also

* [Permissions](permissions.md)
* [Directories](directories.md)
* [MCP Server](../aspnetcore-mcp/index.md)
