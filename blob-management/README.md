# Dignite Blob Management

BLOB management for ABP applications: the tenant's BLOB containers, managed. A BLOB storing provider that applications
use to keep their bytes in it, and - for the people in front of those applications - files and directories in the same
containers, with permissions on a container and on a directory. An infrastructure module, hostable in the
application's own process or as one service.

> **Status: in development, not yet published.** Built: the storage applications use (the provider, the managed
> containers and their presets, the tenant storage quota, content de-duplication, the HTTP API, public read with image
> sizing and tickets) and what people use (uploading and listing files, directories where a container allows them,
> permissions on a container and on a directory, an Angular management page, explorer and form control, MCP tools).

An application keeps its own file metadata and its own authorization:
`container.UseBlobManagement(provider => provider.BlobManagementContainer = "media")` makes any of its BLOB containers
store its bytes in Blob Management, after the application's own pipeline contributors have run. Blob Management
authenticates applications, never their users, on that path; browsers send and fetch files directly with short-lived
tickets the application asks for.

People work in the same containers under Blob Management's own permissions: a role or a user is granted what it may do
in a container, and - where a container has directories - who sees and manages a restricted directory. The application
keeps the id of each file it attaches. `@dignite/ng.blob-management` brings the management page, an explorer and a file
picker a business system opens, and `blob-file-input` for a form; `Dignite.Abp.BlobManagement.Mcp` lets an AI assistant
list, send and organize files.

It is not a cloud drive: there are no personal drives and no sharing of a file with someone. ABP's File Management does
those.

> **.NET 10 · ABP 10.7 · LGPL-3.0-only**

## Documentation

**[Read the documentation](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/index.md)**:

* [Setting Up Blob Management](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/setting-up.md) -
  installing it in a host, declaring its containers in code or in configuration, the tenant quota.
* [Using Blob Management From an Application](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/provider.md) -
  `UseBlobManagement`, Blob Management in the same process or remote, public addresses.
* [Tickets](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/tickets.md) - a browser
  storing or reading a file directly.
* [Hosting Modes](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/hosting.md) - one
  host, or Blob Management as its own service.
* [Attaching Files](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/attaching-files.md) -
  the file API, upload links for users.
* [Directories](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/directories.md) -
  containers with directories, listing, renaming and moving files.
* [Permissions](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/permissions.md) - the
  permissions on the module, on a container and on a directory.
* [Angular UI](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/angular-ui.md) - the
  management page, the explorer, the file picker and the file input.
* [MCP Tools](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/mcp.md) - the `blob_*`
  tools for AI assistants.
* [Importing Directories](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/importing-directories.md) -
  bringing an application's own file library in.
