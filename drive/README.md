# Dignite Drive

A per-tenant cloud drive for ABP applications: a BLOB storing provider that applications use to keep their bytes
in the Drive, plus what people need to attach files - upload one, read it, delete it - hostable in the application's
own process or as one service.

> **Status: in development, not yet published.** Built: the storage applications use (the provider, the Drive
> containers and their presets, the tenant storage quota, content de-duplication, the HTTP API, public read with image
> sizing and tickets) and attaching files for people (user drives, the file API under the Drive's own permissions, an
> Angular form control, MCP tools). The design and the plan are in
> [issue #379](https://github.com/dignite-projects/abp-modules/issues/379).

An application keeps its own file metadata and its own authorization: `container.UseDrive(d => d.DriveContainer = "media")`
makes any of its BLOB containers store its bytes in the Drive, after the application's own pipeline contributors have
run. The Drive authenticates applications, never their users, on that path; browsers send and fetch files directly
with short-lived tickets the application asks for.

People attach files on the same storage: they upload to a user drive under the Drive's own permissions, and the
application keeps the id of each file it attaches. `@dignite/ng.drive` does it from an Angular form with
`drive-file-input`, and `Dignite.Drive.Mcp` lets an AI assistant send a file from the machine it is on.

> **.NET 10 · ABP 10.7 · LGPL-3.0-only**

## Documentation

**[Read the documentation](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/drive/index.md)**:

* [Setting Up the Drive](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/drive/drive-host.md) -
  installing it in a host, declaring its containers in code or in configuration, the tenant quota.
* [Using the Drive From an Application](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/drive/provider.md) -
  `UseDrive`, the Drive in the same process or remote, public addresses.
* [Tickets](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/drive/tickets.md) - a browser storing or
  reading a file directly.
* [Hosting Modes](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/drive/hosting.md) - one host, or
  the Drive as its own service.
* [Attaching Files](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/drive/attaching-files.md) - user
  drives, the file API, upload links for users.
* [Permissions](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/drive/permissions.md) - who may
  upload, read and delete a file.
* [Angular UI](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/drive/angular-ui.md) - the file input
  for forms, and replacing what it shows.
* [MCP Tools](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/drive/mcp.md) - the `drive_*` tools for
  AI assistants.
