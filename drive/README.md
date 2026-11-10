# Dignite Drive

A per-tenant cloud drive for ABP applications: a BLOB storing provider that applications use to keep their bytes
in the Drive, plus a user-facing file library, hostable in the application's own process or as one service.

> **Status: in development, not yet published.** The storage applications use is built: the provider, the Drive
> containers and their presets, the tenant storage quota, content de-duplication, the HTTP API, public read with image
> sizing and tickets. The file library for people comes next. The design and the plan are in
> [issue #379](https://github.com/dignite-projects/abp-modules/issues/379).

An application keeps its own file metadata and its own authorization: `container.UseDrive(d => d.DriveContainer = "media")`
makes any of its BLOB containers store its bytes in the Drive, after the application's own pipeline contributors have
run. The Drive authenticates applications, never their users, on that path; browsers send and fetch files directly
with short-lived tickets the application asks for.

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
