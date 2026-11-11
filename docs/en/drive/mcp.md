```json
//[doc-seo]
{
    "Description": "The Dignite Drive MCP tools: drive_* tools an AI assistant uses to upload a file, read or delete one by its id, upload links that send a file from the machine it is on, and their options."
}
```

# MCP Tools

> Drive has not been released yet; see the [overview](index.md).

`Dignite.Drive.Mcp` contributes the Drive's tools to the application's [MCP server](../aspnetcore-mcp/index.md), so an AI assistant can add files to the Drive for the signed-in user, and read or delete one. Each tool is one call of the [file API](attaching-files.md), with the same [permissions](permissions.md) and rules as the HTTP API.

## Installation

Reference `Dignite.Drive.Mcp` in the host that runs the MCP server, and depend on its module:

````csharp
[DependsOn(typeof(DriveMcpModule))]
public class MyHostModule : AbpModule
{
}
````

The tools call the Drive's application services, in the same process or remote, as the host installs the Drive. They are registered in the `drive` namespace: their names start with `drive_`. A server that maps [several endpoints](../aspnetcore-mcp/hosting.md#several-endpoints) names the endpoint that serves `drive`.

## The Tools

| Tool | |
|---|---|
| `drive_list_containers` | The user drives, with the largest file and the extensions each accepts. Where to start. |
| `drive_create_upload_url` | A one-time link to send a file to, and the command that sends it. |
| `drive_upload_file` | Adds a small file from base64. |
| `drive_get_file` | One file's details, by its id. |
| `drive_delete_file` | Deletes a file by its id (marked destructive). |

No tool lists a drive's files, since the Drive keeps no listing ([Attaching Files](attaching-files.md)): the assistant keeps the id an upload returns, to read or delete the file later. The server's instructions tell it so.

## Sending a File

An assistant would have to write a file's bytes out as text to pass them in a tool call, so a real file goes by a link: `drive_create_upload_url` returns a one-time [upload link](attaching-files.md#upload-links-for-users) and the commands that post the file to it from the machine it is on - `curl -F "content=@FILE_PATH" …` for sh, bash, zsh and cmd.exe, `curl.exe` for PowerShell. The bytes never pass through the assistant, and the upload is authorized again, as the user, when it arrives.

`drive_upload_file` takes the bytes as base64 and suits only a file of a few kilobytes.

## Options

`DriveMcpOptions`:

| Option | Default | |
|---|---|---|
| `BaseUrl` | `null` | The Drive's address, which upload links start with. `null` uses the address the MCP request came in on: right when the MCP server runs in the Drive's host. Set it when the Drive is its own service. |
| `MaxInlineUploadSize` | 2 MB | The largest file `drive_upload_file` takes; the drive's own limit applies too. It fits the MCP endpoint's default body limit (`AbpMcpServerOptions.MaxRequestBodySize`, 4 MB) - raise that as well to raise this. |

````csharp
Configure<DriveMcpOptions>(options =>
{
    options.BaseUrl = "https://drive.example.com";
});
````

## See Also

* [Attaching Files](attaching-files.md)
* [MCP Server](../aspnetcore-mcp/index.md)
