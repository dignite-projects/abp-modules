# Dignite Abp File Storing

> Part of [**dignite-projects/abp-modules**](https://github.com/dignite-projects/abp-modules) — see
> the [repository README](../README.md) for the other modules, and
> [CONTRIBUTING.md](../CONTRIBUTING.md) for the build, versioning, and release process shared across
> them. Formerly developed at `dignite-projects/abp-file-storing`; **no package ID changed** in the
> move.

The file storing and file explorer modules, extracted from `dignite-abp`.

The layout is:

- `core/src/Dignite.Abp.FileStoring`: file upload infrastructure on top of ABP Blob Storing.
- `core/src/Dignite.Abp.FileStoring.Imaging`: optional upload-time image processing.
- `file-explorer/src/Dignite.FileExplorer.*`: DDD file explorer backend.
- `file-explorer/src/Dignite.FileExplorer.Mcp`: optional MCP tools over the file explorer, for AI clients.
- `angular/projects/file-explorer`: Angular UI package.

`dignite-abp` is treated as a frozen source repository and is not modified by this extraction.

## MCP tools (`Dignite.FileExplorer.Mcp`)

Optional. Depend on `FileExplorerMcpModule` to let an AI client use the file explorer through the
application's MCP server (see [`aspnetcore-mcp/`](../aspnetcore-mcp/README.md)). Its tools are named
`file_explorer_*`: list containers, list/create directories, list/get/upload/update/delete files. Every
call goes through the file explorer application services, so each container's own authorization
configuration applies exactly as it does over HTTP.

**Nothing is exposed until the host lists it.** Blob containers cannot be enumerated, and which ones an
AI client should touch is a deployment decision:

```csharp
Configure<FileExplorerMcpOptions>(options =>
{
    options.Containers.Add("site-images", "Images used in site content.");
    options.MaxUploadSize = 5 * 1024 * 1024; // the default
});
```

Uploads travel as base64 inside a JSON-RPC message, so they suit small files only. The module raises the
MCP endpoint's request-body limit just enough for `MaxUploadSize`, so a larger request is refused before it
is read; the container's own size limit applies when it is stricter. File
URLs in tool results point at the HTTP API's file endpoint (`FileExplorerRemoteServiceConsts.FilesRoutePrefix`),
so the host also needs `FileExplorerHttpApiModule` for them to resolve.

## Host secrets

`host/Dignite.FileExplorer.Web.Host/appsettings.json` contains no certificate or encryption passphrases. Configure these values with .NET user-secrets, environment variables, or a secret store instead:

```text
AuthServer:CertificatePassPhrase
StringEncryption:DefaultPassPhrase
Identity:AdminPassword
```

For a first-run Development database, `Identity:AdminPassword` is optional and ABP's development password is used; set the value explicitly before sharing the environment. Non-Development database migration requires `Identity:AdminPassword` and fails when it is missing. For Docker or other deployments, use the corresponding double-underscore environment variable names (for example, `AuthServer__CertificatePassPhrase`).

Data Protection keys are persisted under `DataProtection:KeysPath` (default: `data-protection-keys`). The Docker compose deployment mounts this directory to the durable `host_data_protection_keys` volume. In a multi-instance deployment, point `DataProtection:KeysPath` at a shared durable filesystem available to every API instance.

The seeded `Host_App` OpenIddict client uses Authorization Code with PKCE and Refresh Token grants, plus ABP's Link Login and Impersonation extensions. Password and Client Credentials grants are intentionally disabled because the Angular SPA is a public client and does not need either flow. Swagger uses Authorization Code only.
